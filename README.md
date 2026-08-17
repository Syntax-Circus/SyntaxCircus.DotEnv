# SyntaxCircus.DotEnv

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.DotEnv/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.DotEnv/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.DotEnv.svg)](https://www.nuget.org/packages/SyntaxCircus.DotEnv)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

Reusable local dotenv configuration for ASP.NET Core hosts — and a second entry point for hosts that don't build an `IConfigurationBuilder` at all.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## ASP.NET Core hosts

```csharp
var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration.ShouldLoadDotEnv(builder.Environment))
{
    builder.Configuration.AddSyntaxCircusDotEnvFiles(builder.Environment.ContentRootPath);
}

builder.Configuration.AddUserSecrets<Program>(optional: true);
```

`WebApplication.CreateBuilder()` already adds the real `AddEnvironmentVariables()`/command-line
sources that need to outrank dotenv — don't call `AddEnvironmentVariables()` again after
`AddSyntaxCircusDotEnvFiles`, it's redundant and gains nothing.

The default is enabled in Development and disabled elsewhere. Set `DotEnv:Enabled` to `true` or `false` in normal host configuration to override that default.

When enabled, `.env` is loaded before `.env.local`; `.env.local` overrides it. Both are below process/container environment variables and user secrets. Keys using `__` map to the normal `:` configuration path separator.

### Precedence, worked examples

From lowest to highest priority (last one wins), for the standard registration shown above:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. `.env`
4. `.env.local`
5. Real process/container environment variables (`AddEnvironmentVariables()`)
6. Command-line arguments
7. `AddUserSecrets<T>()`

Given a key `Foo` set in some combination of these sources:

| appsettings.json  | .env       | .env.local   | process env var | `configuration["Foo"]` resolves to | Why                                           |
|--------------------|------------|--------------|------------------|--------------------------------------|------------------------------------------------|
| `from-appsettings` | —          | —            | —                | `from-appsettings`                   | only source with a value                        |
| `from-appsettings` | `from-env` | —            | —                | `from-env`                           | `.env` overrides `appsettings.json`             |
| `from-appsettings` | `from-env` | `from-local` | —                | `from-local`                         | `.env.local` overrides `.env`                   |
| `from-appsettings` | `from-env` | `from-local` | `from-process`   | `from-process`                       | real process env vars always win over dotenv    |

With `hostPrefix`/`knownHostPrefixes` (see below), the same table applies to keys with no
matching prefix; a key like `MyAppApi__Foo` in a shared `.env` only participates when
`hostPrefix: "MyAppApi__"` is passed for that host.

### Monorepo hosts sharing one `.env` file

If several hosts in the same repo (an API, a Web app, a Worker, ...) share one `.env` file, pass a `hostPrefix` so each host only picks up its own overrides plus whatever isn't prefixed at all:

```csharp
builder.Configuration.AddSyntaxCircusDotEnvFiles(
    builder.Environment.ContentRootPath,
    hostPrefix: "MyAppApi__",
    knownHostPrefixes: ["MyAppApi__", "MyAppWeb__", "MyAppWorker__"]);
```

A key like `MyAppApi__ConnectionStrings__Default=...` only applies when `hostPrefix` is `"MyAppApi__"` (with the prefix stripped before the `__` → `:` mapping); a key like `Logging__LogLevel__Default=Information` with no matching prefix applies everywhere.

## Non-ASP.NET-Core hosts (console apps, workers)

For hosts that set up their own environment before anything reads `IConfiguration` — or that don't use `Microsoft.Extensions.Configuration` at all — call `DotEnvProcessLoader` early in `Main`, before anything reads environment variables:

```csharp
DotEnvProcessLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);
```

This walks up to the nearest `.git` directory, then sets real process environment variables from `.env` and `.env.local` (never overwriting a variable that was already set before either file loaded; `.env.local` overrides `.env`).

### Precedence, worked examples

`LoadFromRepositoryRoot` never overwrites a process environment variable that existed
*before the call*; `.env.local` overwrites `.env` for anything not already set:

| process env var (before call) | .env       | .env.local   | `Environment.GetEnvironmentVariable("Foo")` after call | Why                                       |
|---------------------------------|------------|--------------|------------------------------------------------------------|----------------------------------------------|
| —                                | `from-env` | —            | `from-env`                                                  | only source with a value                      |
| —                                | `from-env` | `from-local` | `from-local`                                                | `.env.local` overrides `.env`                 |
| `original`                       | `from-env` | `from-local` | `original`                                                  | pre-existing process env var always wins      |

## Known limitations

`AddSyntaxCircusDotEnvFiles` decides where to insert dotenv values by scanning
`IConfigurationBuilder.Sources`: it finds the last source that *isn't* an
`EnvironmentVariablesConfigurationSource`/`CommandLineConfigurationSource`/`ChainedConfigurationSource`,
and inserts right after it — landing before whichever of those three comes next. This correctly
matches `WebApplicationBuilder`/`HostApplicationBuilder`'s default source shape, with or without
command-line args, on current .NET.

That said, it's a heuristic over an internal, unversioned detail of the Generic Host — not a
documented contract — so it comes with real caveats:

- A future .NET release could change how many sources of each type the host adds, or their
  order, and silently change where dotenv values land.
- Other configuration providers added before this call in unusual positions — Azure App
  Configuration, Key Vault, Consul, a YAML/XML/INI file loaded after `AddEnvironmentVariables()`
  on purpose — aren't something this heuristic can reason about. It handles "some base-config
  source, then the real environment/command-line sources" correctly regardless of the base
  config's format; it can get confused if environment-variable/command-line sources and
  base-config sources are deliberately interleaved in a non-default order.
- If you're unsure for your specific host, dump the source list before and after calling
  `AddSyntaxCircusDotEnvFiles` and confirm the dotenv `MemoryConfigurationSource`(s) land where
  you expect:
  ```csharp
  Console.WriteLine(string.Join(", ", builder.Configuration.Sources.Select(s => s.GetType().Name)));
  ```

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
