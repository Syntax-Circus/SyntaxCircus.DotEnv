using SyntaxCircus.DotEnv.Tests.Infrastructure;

namespace SyntaxCircus.DotEnv.Tests;

public sealed class AddSyntaxCircusProjectDotEnvFilesTests : IDisposable
{
    private readonly TempDirectory _tempDirectory = new();

    public void Dispose() => _tempDirectory.Dispose();

    private string ProjectPath => Path.Combine(_tempDirectory.Path, "project");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MissingProjectFiles_DoNotFallBackToParentOrSibling(bool hasEnv, bool hasLocal)
    {
        _tempDirectory.WriteFile(".env", "ParentEnv=must-not-load\n");
        _tempDirectory.WriteFile(".env.local", "ParentLocal=must-not-load\n");
        _tempDirectory.WriteFile("sibling/.env", "SiblingEnv=must-not-load\n");
        _tempDirectory.WriteFile("sibling/.env.local", "SiblingLocal=must-not-load\n");
        Directory.CreateDirectory(ProjectPath);
        if (hasEnv)
        {
            _tempDirectory.WriteFile("project/.env", "ProjectEnv=from-env\nShared=from-env\n");
        }
        if (hasLocal)
        {
            _tempDirectory.WriteFile("project/.env.local", "ProjectLocal=from-local\nShared=from-local\n");
        }

        var configuration = new ConfigurationBuilder()
            .AddSyntaxCircusProjectDotEnvFiles(ProjectPath)
            .Build();

        configuration["ParentEnv"].ShouldBeNull();
        configuration["ParentLocal"].ShouldBeNull();
        configuration["SiblingEnv"].ShouldBeNull();
        configuration["SiblingLocal"].ShouldBeNull();
        configuration["ProjectEnv"].ShouldBe(hasEnv ? "from-env" : null);
        configuration["ProjectLocal"].ShouldBe(hasLocal ? "from-local" : null);
        configuration["Shared"].ShouldBe(hasLocal ? "from-local" : hasEnv ? "from-env" : null);
    }

    [Fact]
    public void HostPrefixes_FilterAndOverrideGenericKeysBeforeMapping()
    {
        _tempDirectory.WriteFile("project/.env", "Section__Child=generic\nApi__Section__Child=api\nWeb__Section__Child=web\nGeneric__Child=shared\n");

        var configuration = new ConfigurationBuilder()
            .AddSyntaxCircusProjectDotEnvFiles(ProjectPath, "Api__", ["Api__", "Web__"])
            .Build();

        configuration["Section:Child"].ShouldBe("api");
        configuration["Generic:Child"].ShouldBe("shared");
        configuration["Api:Section:Child"].ShouldBeNull();
        configuration["Web:Section:Child"].ShouldBeNull();
    }

    [Fact]
    public void ProjectFiles_OverrideBaseConfigurationButLoseToProcessAndCommandLine()
    {
        var key = $"SC_PROJECT_{Guid.NewGuid():N}";
        var processKey = key + "_PROCESS";
        var commandKey = key + "_COMMAND";
        _tempDirectory.WriteFile("project/.env", $"{key}=env\n{processKey}=env\n{commandKey}=env\n");
        _tempDirectory.WriteFile("project/.env.local", $"{key}=local\n{processKey}=local\n{commandKey}=local\n");
        var originalProcessValue = Environment.GetEnvironmentVariable(processKey);
        var originalCommandValue = Environment.GetEnvironmentVariable(commandKey);
        try
        {
            Environment.SetEnvironmentVariable(processKey, "process");
            Environment.SetEnvironmentVariable(commandKey, "process");
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [key] = "base", [processKey] = "base", [commandKey] = "base"
                })
                .AddEnvironmentVariables()
                .AddCommandLine([$"--{commandKey}=command"]);

            var configuration = builder.AddSyntaxCircusProjectDotEnvFiles(ProjectPath).Build();

            configuration[key].ShouldBe("local");
            configuration[processKey].ShouldBe("process");
            configuration[commandKey].ShouldBe("command");
            Environment.GetEnvironmentVariable(key).ShouldBeNull();
            Environment.GetEnvironmentVariable(processKey).ShouldBe("process");
            Environment.GetEnvironmentVariable(commandKey).ShouldBe("process");
        }
        finally
        {
            Environment.SetEnvironmentVariable(processKey, originalProcessValue);
            Environment.SetEnvironmentVariable(commandKey, originalCommandValue);
        }
    }

    [Fact]
    public void LegacyApi_MissingFilesStillTraverseParents()
    {
        _tempDirectory.WriteFile(".env", "LegacyEnv=from-parent-env\n");
        _tempDirectory.WriteFile(".env.local", "LegacyLocal=from-parent-local\n");
        Directory.CreateDirectory(ProjectPath);

        var configuration = new ConfigurationBuilder().AddSyntaxCircusDotEnvFiles(ProjectPath).Build();

        configuration["LegacyEnv"].ShouldBe("from-parent-env");
        configuration["LegacyLocal"].ShouldBe("from-parent-local");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("project")]
    [InlineData("../project")]
    public void NonAbsoluteProjectDirectory_ThrowsArgumentException(string projectDirectory)
    {
        Should.Throw<ArgumentException>(() =>
            new ConfigurationBuilder().AddSyntaxCircusProjectDotEnvFiles(projectDirectory));
    }

    [Fact]
    public void NullBuilder_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            DotEnvConfigurationExtensions.AddSyntaxCircusProjectDotEnvFiles(null!, ProjectPath));
    }
}
