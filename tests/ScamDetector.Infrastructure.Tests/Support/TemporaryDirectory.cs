namespace ScamDetector.Infrastructure.Tests.Support;

public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scamdetector-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void WriteFile(string fileName, string content, DateTime lastWriteTimeUtc)
    {
        var filePath = System.IO.Path.Combine(Path, fileName);
        File.WriteAllText(filePath, content);
        File.SetLastWriteTimeUtc(filePath, lastWriteTimeUtc);
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
