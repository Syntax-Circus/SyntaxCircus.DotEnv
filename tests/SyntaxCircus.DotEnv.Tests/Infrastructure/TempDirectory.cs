namespace SyntaxCircus.DotEnv.Tests.Infrastructure;

/// <summary>A throwaway directory tree for tests that need real file I/O, deleted on Dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sc-dotenv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public void WriteFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
