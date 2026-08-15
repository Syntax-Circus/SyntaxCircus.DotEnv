using DotNetEnv;

namespace SyntaxCircus.DotEnv;

/// <summary>
/// Loads <c>.env</c>/<c>.env.local</c> directly into the process environment, for hosts that
/// don't use <see cref="Microsoft.Extensions.Configuration.IConfigurationBuilder"/> (console
/// apps, workers). Walks up from <paramref name="currentDirectory"/> to find the repository root
/// (a <c>.git</c> directory), then applies precedence: variables already present in the process
/// environment before either file is loaded always win; <c>.env.local</c> wins over <c>.env</c>.
/// </summary>
public static class DotEnvProcessLoader
{
    public static void LoadFromRepositoryRoot(string currentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var repositoryRoot = FindRepositoryRoot(currentDirectory);
        var originalKeys = Environment.GetEnvironmentVariables()
            .Keys
            .Cast<object>()
            .Select(key => key.ToString())
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        LoadFile(Path.Combine(repositoryRoot, ".env"), originalKeys, allowOverride: false);
        LoadFile(Path.Combine(repositoryRoot, ".env.local"), originalKeys, allowOverride: true);
    }

    private static void LoadFile(string path, HashSet<string> originalKeys, bool allowOverride)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var (key, value) in Env.NoEnvVars().Load(path))
        {
            if (originalKeys.Contains(key))
            {
                continue;
            }

            if (!allowOverride && Environment.GetEnvironmentVariable(key) is not null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string FindRepositoryRoot(string currentDirectory)
    {
        var directory = new DirectoryInfo(currentDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return currentDirectory;
    }
}
