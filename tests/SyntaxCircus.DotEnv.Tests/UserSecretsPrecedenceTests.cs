using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration.UserSecrets;
using SyntaxCircus.DotEnv.Tests.Infrastructure;

[assembly: UserSecretsId(SyntaxCircus.DotEnv.Tests.UserSecretsPrecedenceTests.UserSecretsId)]

namespace SyntaxCircus.DotEnv.Tests;

// Pins how dotenv ranks against user secrets. WebApplication.CreateBuilder registers the
// Development user-secrets source for the application assembly (here: this test assembly, via
// the UserSecretsId attribute above) BEFORE dotenv is inserted, so dotenv overrides it. An
// explicit AddUserSecrets call made AFTER AddSyntaxCircusDotEnvFiles is appended last and wins.
// A secrets.json is written under the real user-secrets location for a test-only id, and removed on dispose.
public sealed class UserSecretsPrecedenceTests : IDisposable
{
    public const string UserSecretsId = "5d0b1b32-6f0e-4e43-9c52-syntaxcircus-dotenv-tests";

    private readonly TempDirectory _tempDirectory = new();
    private readonly string _secretsDirectory = GetSecretsDirectory();

    public void Dispose()
    {
        _tempDirectory.Dispose();
        if (Directory.Exists(_secretsDirectory))
        {
            Directory.Delete(_secretsDirectory, recursive: true);
        }
    }

    // Mirrors the framework's PathHelper (it is internal).
    private static string GetSecretsDirectory()
    {
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        return appData is not null
            ? Path.Combine(appData, "Microsoft", "UserSecrets", UserSecretsId)
            : Path.Combine(Environment.GetEnvironmentVariable("HOME")!, ".microsoft", "usersecrets", UserSecretsId);
    }

    private static string UniqueKey(string suffix) => $"SC_TEST_{Guid.NewGuid():N}_{suffix}";

    private void WriteSecrets(string key, string value)
    {
        Directory.CreateDirectory(_secretsDirectory);
        File.WriteAllText(Path.Combine(_secretsDirectory, "secrets.json"), $$"""{"{{key}}": "{{value}}"}""");
    }

    private WebApplicationBuilder CreateDevelopmentBuilder() => WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ContentRootPath = _tempDirectory.Path,
        ApplicationName = typeof(UserSecretsPrecedenceTests).Assembly.GetName().Name,
        EnvironmentName = "Development",
    });

    [Fact]
    public void FrameworkAddedUserSecrets_LoseToDotEnv()
    {
        var key = UniqueKey("FRAMEWORK_SECRETS");
        WriteSecrets(key, "from-user-secrets");
        _tempDirectory.WriteFile(".env", $"{key}=from-dotenv\n");

        var builder = CreateDevelopmentBuilder();

        // Sanity check: the framework really did register the user secrets before dotenv is added.
        builder.Configuration[key].ShouldBe("from-user-secrets");

        builder.Configuration.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);

        builder.Configuration[key].ShouldBe("from-dotenv");
    }

    [Fact]
    public void ExplicitAddUserSecretsAfterDotEnv_WinsOverDotEnv()
    {
        var key = UniqueKey("EXPLICIT_SECRETS");
        WriteSecrets(key, "from-user-secrets");
        _tempDirectory.WriteFile(".env", $"{key}=from-dotenv\n");

        var builder = CreateDevelopmentBuilder();
        builder.Configuration.AddSyntaxCircusDotEnvFiles(_tempDirectory.Path);
        builder.Configuration.AddUserSecrets(UserSecretsId);

        builder.Configuration[key].ShouldBe("from-user-secrets");
    }
}
