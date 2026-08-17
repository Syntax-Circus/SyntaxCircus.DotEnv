using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Hosting;

namespace SyntaxCircus.DotEnv;

public sealed class DotEnvOptions
{
    public bool? Enabled { get; init; }
}

public static class DotEnvConfigurationExtensions
{
    private static readonly string[] DotEnvFileNames = [".env", ".env.local"];

    public static bool ShouldLoadDotEnv(this IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        return configuration.GetValue<bool?>("DotEnv:Enabled") ?? environment.IsDevelopment();
    }

    public static IConfigurationBuilder AddSyntaxCircusDotEnvFiles(this IConfigurationBuilder configurationBuilder, string basePath)
        => AddSyntaxCircusDotEnvFiles(configurationBuilder, basePath, hostPrefix: null, knownHostPrefixes: null);

    /// <summary>
    /// Loads <c>.env</c>/<c>.env.local</c> the same way as the single-argument overload, but when
    /// <paramref name="hostPrefix"/> is supplied, keys starting with any prefix in
    /// <paramref name="knownHostPrefixes"/> (defaulting to just <paramref name="hostPrefix"/>
    /// itself when not supplied) are treated as host-specific: kept only if they start with
    /// <paramref name="hostPrefix"/>, with that prefix stripped before the <c>__</c> → <c>:</c>
    /// mapping is applied. Keys that don't start with any known prefix are always treated as
    /// generic and applied regardless of host. This lets one shared <c>.env</c> file carry
    /// per-host overrides for every host in a monorepo — e.g. <c>MyAppApi__ConnectionStrings__Default=...</c>
    /// only applies when <paramref name="hostPrefix"/> is <c>"MyAppApi__"</c>.
    /// </summary>
    public static IConfigurationBuilder AddSyntaxCircusDotEnvFiles(
        this IConfigurationBuilder configurationBuilder,
        string basePath,
        string? hostPrefix,
        IReadOnlyCollection<string>? knownHostPrefixes = null)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        IReadOnlyCollection<string> reservedPrefixes = knownHostPrefixes is { Count: > 0 }
            ? knownHostPrefixes
            : string.IsNullOrWhiteSpace(hostPrefix) ? [] : [hostPrefix];

        var insertIndex = FindEnvironmentOverrideInsertIndex(configurationBuilder);
        foreach (var fileName in DotEnvFileNames)
        {
            var pairs = LoadMappedPairs(basePath, fileName, hostPrefix, reservedPrefixes);

            if (pairs.Count != 0)
            {
                configurationBuilder.Sources.Insert(insertIndex++, new MemoryConfigurationSource { InitialData = pairs });
            }
        }

        return configurationBuilder;
    }

    private static List<KeyValuePair<string, string?>> LoadMappedPairs(
        string basePath,
        string fileName,
        string? hostPrefix,
        IReadOnlyCollection<string> reservedPrefixes)
    {
        var generic = new List<KeyValuePair<string, string?>>();
        var hostSpecific = new List<KeyValuePair<string, string?>>();

        foreach (var pair in Env.NoEnvVars().TraversePath().Load(Path.Combine(basePath, fileName)))
        {
            if (!TryMapKey(pair.Key, hostPrefix, reservedPrefixes, out var mappedKey, out var isHostSpecific))
            {
                continue;
            }

            var mapped = new KeyValuePair<string, string?>(mappedKey!, pair.Value);
            (isHostSpecific ? hostSpecific : generic).Add(mapped);
        }

        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in generic.Concat(hostSpecific))
        {
            merged[pair.Key] = pair.Value;
        }

        return [.. merged];
    }

    private static bool TryMapKey(
        string key,
        string? hostPrefix,
        IReadOnlyCollection<string> reservedPrefixes,
        out string? mappedKey,
        out bool isHostSpecific)
    {
        mappedKey = null;
        isHostSpecific = false;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        foreach (var reservedPrefix in reservedPrefixes)
        {
            if (!key.StartsWith(reservedPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(hostPrefix) || !key.StartsWith(hostPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            mappedKey = NormalizeKey(key[hostPrefix.Length..]);
            isHostSpecific = true;
            return !string.IsNullOrWhiteSpace(mappedKey);
        }

        mappedKey = NormalizeKey(key);
        return !string.IsNullOrWhiteSpace(mappedKey);
    }

    private static string NormalizeKey(string key) => key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);

    private static int FindEnvironmentOverrideInsertIndex(IConfigurationBuilder configurationBuilder)
    {
        var sources = configurationBuilder.Sources;

        // Dotenv values belong right after the last "base config" source (appsettings.json,
        // any other file/memory-backed config) and right before whatever environment-variable
        // or command-line source comes next. WebApplicationBuilder/HostApplicationBuilder add
        // TWO of each kind: an early one (added before appsettings.json, used internally to
        // resolve ASPNETCORE_ENVIRONMENT/content root/etc.) and a "real" one added after
        // appsettings.json that's meant to win over it. Scanning for the last non-env/cmdline
        // source and inserting right after it lands on the real one in both cases, whether or
        // not command-line args are present (ChainedConfigurationSource is excluded from the
        // "base config" scan because WebApplicationBuilder appends one at the very end, after
        // both real sources).
        var lastBaseConfigIndex = -1;
        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index].GetType().Name is not ("EnvironmentVariablesConfigurationSource" or "CommandLineConfigurationSource" or "ChainedConfigurationSource"))
            {
                lastBaseConfigIndex = index;
            }
        }

        for (var index = lastBaseConfigIndex + 1; index < sources.Count; index++)
        {
            if (sources[index].GetType().Name is "EnvironmentVariablesConfigurationSource" or "CommandLineConfigurationSource")
            {
                return index;
            }
        }

        return sources.Count;
    }
}
