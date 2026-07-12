using System.Diagnostics;

namespace Decorator.V2;

/// <summary>
/// Decorator: Adds logging around ANY IFileRepository.
/// Works with S3, Local, Azure — or even other decorators!
/// </summary>
public class LoggingDecorator : IFileRepository
{
    private readonly IFileRepository _inner;

    public LoggingDecorator(IFileRepository inner)
    {
        _inner = inner;
    }

    public void Upload(string fileName, byte[] content)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  [LOG] Upload started: '{fileName}' ({content.Length} bytes)");
        _inner.Upload(fileName, content);
        Console.WriteLine($"  [LOG] Upload completed in {sw.ElapsedMilliseconds}ms");
    }

    public byte[] Download(string fileName)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  [LOG] Download started: '{fileName}'");
        var result = _inner.Download(fileName);
        Console.WriteLine($"  [LOG] Download completed in {sw.ElapsedMilliseconds}ms ({result.Length} bytes)");
        return result;
    }

    public void Delete(string fileName)
    {
        Console.WriteLine($"  [LOG] Delete started: '{fileName}'");
        _inner.Delete(fileName);
        Console.WriteLine($"  [LOG] Delete completed");
    }
}
