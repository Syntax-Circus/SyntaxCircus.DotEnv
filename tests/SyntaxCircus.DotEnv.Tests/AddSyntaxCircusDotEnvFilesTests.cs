using SyntaxCircus.DotEnv.Tests.Infrastructure;

namespace SyntaxCircus.DotEnv.Tests;

// These tests write real .env files to a per-test temp directory and load them for real via
// DotNetEnv. Loading also has a side effect on the real process environment (via Env.NoClobber()
// internally) — every test uses a Guid-prefixed variable name so it can never collide with
// anything real, and unsets what it set in a finally block.
public sealed class AddSyntaxCircusDotEnvFilesTests : IDisposable
{
    private readonly TempDirectory _tempDirectory = new();
    private readonly List<string> _envVarsToClean = [];

    public void Dispose()
    {
        foreach (var key in _envVarsToClean)
        {
            Environment.SetEnvironmentVariable(key, null);
        }

        _tempDirectory.Dispose();
    }

    private static string UniqueKey(string suffix) => $"SC_TEST_{Guid.NewGuid():N}_{suffix}";

    [Fact]
    public void NullBuilder_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            DotEnvConfigurationExtensions.AddSyntaxCircusDotEnvFiles(null!, _tempDirectory.Path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankBasePath_ThrowsArgumentException(string basePath)
    {
        var builder = new ConfigurationBuilder();

        Should.Throw<ArgumentException>(() => builder.AddSyntaxCircusDotEnvFiles(basePath));
    }

    [Fact]
    public void NoFilesPresent_NoSourcesAdded()
    {
        var builder = new ConfigurationBuilder();
        var countBefore = builder.Sources.Count;

        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);

        builder.Sources.Count.ShouldBe(countBefore);
    }

    [Fact]
    public void OnlyDotEnv_ValueApplied()
    {
        var key = UniqueKey("ONLY_ENV");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=from-env\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);
        var configuration = builder.Build();

        configuration[key].ShouldBe("from-env");
    }

    [Fact]
    public void OnlyDotEnvLocal_ValueApplied()
    {
        var key = UniqueKey("ONLY_LOCAL");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env.local", $"{key}=from-local\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);
        var configuration = builder.Build();

        configuration[key].ShouldBe("from-local");
    }

    [Fact]
    public void BothFiles_LocalWinsOverEnv()
    {
        var key = UniqueKey("BOTH");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=from-env\n");
        _tempDirectory.WriteFile(".env.local", $"{key}=from-local\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);
        var configuration = builder.Build();

        configuration[key].ShouldBe("from-local");
    }

    [Fact]
    public void DoubleUnderscore_MapsToConfigurationPathDelimiter()
    {
        var prefix = UniqueKey("SECTION");
        _envVarsToClean.Add($"{prefix}__Child");
        _tempDirectory.WriteFile(".env", $"{prefix}__Child=nested-value\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);
        var configuration = builder.Build();

        configuration[$"{prefix}:Child"].ShouldBe("nested-value");
    }

    [Fact]
    public void NoHostPrefix_AllKeysTreatedAsGeneric()
    {
        var key = UniqueKey("GENERIC");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=generic-value\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path, hostPrefix: null);
        var configuration = builder.Build();

        configuration[key].ShouldBe("generic-value");
    }

    [Fact]
    public void HostPrefix_MatchingKey_PrefixStrippedAndApplied()
    {
        var suffix = UniqueKey("HOSTKEY");
        var hostPrefix = "ApiHost__";
        _envVarsToClean.Add(suffix);
        _tempDirectory.WriteFile(".env", $"{hostPrefix}{suffix}=host-specific-value\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path, hostPrefix);
        var configuration = builder.Build();

        configuration[suffix].ShouldBe("host-specific-value");
    }

    [Fact]
    public void HostPrefix_KeyMatchingDifferentKnownPrefix_Excluded()
    {
        var suffix = UniqueKey("OTHERHOST");
        var otherHostPrefix = "WebHost__";
        var thisHostPrefix = "ApiHost__";
        _tempDirectory.WriteFile(".env", $"{otherHostPrefix}{suffix}=should-not-apply\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path, thisHostPrefix, [thisHostPrefix, otherHostPrefix]);
        var configuration = builder.Build();

        configuration[suffix].ShouldBeNull();
        configuration[$"{otherHostPrefix}{suffix}"].ShouldBeNull();
    }

    [Fact]
    public void KeyMatchingNoKnownPrefix_AlwaysAppliedAsGeneric()
    {
        var key = UniqueKey("UNPREFIXED");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=always-applies\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path, "ApiHost__", ["ApiHost__", "WebHost__"]);
        var configuration = builder.Build();

        configuration[key].ShouldBe("always-applies");
    }

    [Fact]
    public void DotEnvSources_InsertedBeforeEnvironmentVariablesSource()
    {
        var key = UniqueKey("ORDER");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=dotenv-value\n");

        var builder = new ConfigurationBuilder();
        builder.AddEnvironmentVariables();

        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);

        var memorySourceIndex = builder.Sources
            .Select((source, index) => (source, index))
            .First(x => x.source is Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource)
            .index;
        var environmentSourceIndex = builder.Sources
            .Select((source, index) => (source, index))
            .First(x => x.source.GetType().Name == "EnvironmentVariablesConfigurationSource")
            .index;

        memorySourceIndex.ShouldBeLessThan(environmentSourceIndex);
    }
}
