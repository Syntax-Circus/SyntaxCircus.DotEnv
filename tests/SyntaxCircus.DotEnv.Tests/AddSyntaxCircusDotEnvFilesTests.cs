using Microsoft.AspNetCore.Builder;
using SyntaxCircus.DotEnv.Tests.Infrastructure;

namespace SyntaxCircus.DotEnv.Tests;

// These tests write real .env files to a per-test temp directory and load them for real via
// DotNetEnv. AddSyntaxCircusDotEnvFiles only ever writes into the given IConfigurationBuilder —
// it never touches the real process environment — so no Environment cleanup is needed here.
public sealed class AddSyntaxCircusDotEnvFilesTests : IDisposable
{
    private readonly TempDirectory _tempDirectory = new();

    public void Dispose() => _tempDirectory.Dispose();

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

    [Fact]
    public void MultipleEnvironmentVariablesSources_DotEnvInsertedAfterLastOne()
    {
        // Shaped like WebApplicationBuilder.Configuration.Sources: an early
        // EnvironmentVariablesConfigurationSource added by host bootstrap, then an
        // appsettings.json-equivalent source, then the "real" EnvironmentVariablesConfigurationSource
        // that's meant to win over appsettings.json. Dotenv should be inserted right before the
        // *last* one, so it overrides the appsettings-equivalent source but still loses to real
        // process env vars.
        var key = UniqueKey("MULTIENV");
        _tempDirectory.WriteFile(".env.local", $"{key}=from-dotenv\n");

        var builder = new ConfigurationBuilder();
        builder.AddEnvironmentVariables();
        builder.AddInMemoryCollection(new Dictionary<string, string?> { [key] = "from-appsettings" });
        builder.AddEnvironmentVariables();

        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);
        var configuration = builder.Build();

        configuration[key].ShouldBe("from-dotenv");
    }

    [Fact]
    public void WebApplicationBuilder_DotEnvOverridesAppSettingsJson()
    {
        var key = UniqueKey("WEBAPP");
        _tempDirectory.WriteFile("appsettings.json", $$"""{"{{key}}": "from-appsettings"}""");
        _tempDirectory.WriteFile(".env.local", $"{key}=from-dotenv\n");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = _tempDirectory.Path,
            EnvironmentName = "Development",
        });

        builder.Configuration.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);

        builder.Configuration[key].ShouldBe("from-dotenv");
    }

    [Fact]
    public void CalledMultipleTimes_DoesNotMutateRealProcessEnvironment()
    {
        var keyA = UniqueKey("NOMUTATE_A");
        var keyB = UniqueKey("NOMUTATE_B");
        using var otherTempDirectory = new TempDirectory();
        _tempDirectory.WriteFile(".env", $"{keyA}=value-a\n");
        otherTempDirectory.WriteFile(".env", $"{keyB}=value-b\n");

        var builder = new ConfigurationBuilder();
        builder.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);
        builder.AddSyntaxCircusDotEnvFiles(otherTempDirectory.Path);

        Environment.GetEnvironmentVariable(keyA).ShouldBeNull();
        Environment.GetEnvironmentVariable(keyB).ShouldBeNull();
    }
}
