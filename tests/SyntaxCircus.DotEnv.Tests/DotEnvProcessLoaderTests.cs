using SyntaxCircus.DotEnv.Tests.Infrastructure;

namespace SyntaxCircus.DotEnv.Tests;

// LoadFromRepositoryRoot mutates the real process environment. Every test uses a Guid-prefixed
// variable name so it can never collide with anything real, and unsets what it set afterward.
public sealed class DotEnvProcessLoaderTests : IDisposable
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankCurrentDirectory_ThrowsArgumentException(string currentDirectory)
    {
        Should.Throw<ArgumentException>(() => DotEnvProcessLoader.LoadFromRepositoryRoot(currentDirectory));
    }

    [Fact]
    public void MissingFiles_NoOp()
    {
        Should.NotThrow(() => DotEnvProcessLoader.LoadFromRepositoryRoot(_tempDirectory.Path));
    }

    [Fact]
    public void EnvFile_AtGivenDirectory_SetsEnvironmentVariable()
    {
        var key = UniqueKey("PLAIN");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=from-env\n");

        DotEnvProcessLoader.LoadFromRepositoryRoot(_tempDirectory.Path);

        Environment.GetEnvironmentVariable(key).ShouldBe("from-env");
    }

    [Fact]
    public void GitMarker_FoundWalkingUpFromNestedDirectory_UsesRootEnvFile()
    {
        var key = UniqueKey("REPOROOT");
        _envVarsToClean.Add(key);
        Directory.CreateDirectory(Path.Combine(_tempDirectory.Path, ".git"));
        _tempDirectory.WriteFile(".env", $"{key}=from-repo-root\n");
        var nested = Path.Combine(_tempDirectory.Path, "src", "sub");
        Directory.CreateDirectory(nested);

        DotEnvProcessLoader.LoadFromRepositoryRoot(nested);

        Environment.GetEnvironmentVariable(key).ShouldBe("from-repo-root");
    }

    [Fact]
    public void NoGitMarkerFound_FallsBackToGivenDirectory()
    {
        var key = UniqueKey("NOFALLBACK");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=from-given-dir\n");

        // No .git anywhere under _tempDirectory.Path, so FindRepositoryRoot walks up to a real
        // ancestor (none of which has a marker either in this isolated temp tree) and returns
        // the original directory unchanged.
        DotEnvProcessLoader.LoadFromRepositoryRoot(_tempDirectory.Path);

        Environment.GetEnvironmentVariable(key).ShouldBe("from-given-dir");
    }

    [Fact]
    public void KeyAlreadyInProcessEnvironment_NeitherFileOverridesIt()
    {
        var key = UniqueKey("PREEXISTING");
        _envVarsToClean.Add(key);
        Environment.SetEnvironmentVariable(key, "original-value");
        _tempDirectory.WriteFile(".env", $"{key}=from-env\n");
        _tempDirectory.WriteFile(".env.local", $"{key}=from-local\n");

        DotEnvProcessLoader.LoadFromRepositoryRoot(_tempDirectory.Path);

        Environment.GetEnvironmentVariable(key).ShouldBe("original-value");
    }

    [Fact]
    public void EnvLocal_OverridesEnv_ForKeyNotPreexisting()
    {
        var key = UniqueKey("LOCALWINS");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env", $"{key}=from-env\n");
        _tempDirectory.WriteFile(".env.local", $"{key}=from-local\n");

        DotEnvProcessLoader.LoadFromRepositoryRoot(_tempDirectory.Path);

        Environment.GetEnvironmentVariable(key).ShouldBe("from-local");
    }

    [Fact]
    public void OnlyEnvLocal_ValueApplied()
    {
        var key = UniqueKey("ONLYLOCAL");
        _envVarsToClean.Add(key);
        _tempDirectory.WriteFile(".env.local", $"{key}=from-local\n");

        DotEnvProcessLoader.LoadFromRepositoryRoot(_tempDirectory.Path);

        Environment.GetEnvironmentVariable(key).ShouldBe("from-local");
    }
}
