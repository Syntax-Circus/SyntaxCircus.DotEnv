# SyntaxCircus.DotEnv

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.DotEnv/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.DotEnv/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.DotEnv.svg)](https://www.nuget.org/packages/SyntaxCircus.DotEnv)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

Reusable local dotenv configuration for ASP.NET Core hosts — and a second entry point for hosts that don't build an `IConfigurationBuilder` at all.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## ASP.NET Core hosts

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);

if (builder.Configuration.ShouldLoadDotEnv(builder.Environment))
{
    builder.Configuration.AddSyntaxCircusDotEnvFiles(builder.Environment.ContentRootPath);
    builder.Configuration.AddEnvironmentVariables();
}

builder.Configuration.AddUserSecrets<Program>(optional: true);
```

The default is enabled in Development and disabled elsewhere. Set `DotEnv:Enabled` to `true` or `false` in normal host configuration to override that default.

When enabled, `.env` is loaded before `.env.local`; `.env.local` overrides it. Both are below process/container environment variables and user secrets. Keys using `__` map to the normal `:` configuration path separator.

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

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
