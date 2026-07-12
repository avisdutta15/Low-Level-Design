using System.Diagnostics;

namespace Decorator.V1;

/// <summary>
/// Even worse: combining two concerns in one class.
/// What about Logging + Encryption? Caching + Retry? All four?
/// 2^N combinations needed!
/// </summary>
public class S3FileRepositoryWithLoggingAndCaching : IFileRepository
{
    private readonly Dictionary<string, byte[]> _cache = new();

    public void Upload(string fileName, byte[] content)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  [LOG] Upload started: '{fileName}'");
        Console.WriteLine($"  [S3] Uploading '{fileName}' ({content.Length} bytes)");
        _cache[fileName] = content;
        Console.WriteLine($"  [CACHE] Cached '{fileName}'");
        Console.WriteLine($"  [LOG] Upload completed in {sw.ElapsedMilliseconds}ms");
    }

    public byte[] Download(string fileName)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  [LOG] Download started: '{fileName}'");

        if (_cache.TryGetValue(fileName, out var cached))
        {
            Console.WriteLine($"  [CACHE] Cache hit for '{fileName}'");
            Console.WriteLine($"  [LOG] Download completed in {sw.ElapsedMilliseconds}ms (from cache)");
            return cached;
        }

        Console.WriteLine($"  [S3] Downloading '{fileName}'");
        var data = new byte[] { 1, 2, 3 };
        _cache[fileName] = data;
        Console.WriteLine($"  [CACHE] Cached '{fileName}'");
        Console.WriteLine($"  [LOG] Download completed in {sw.ElapsedMilliseconds}ms");
        return data;
    }

    public void Delete(string fileName)
    {
        Console.WriteLine($"  [LOG] Delete started: '{fileName}'");
        Console.WriteLine($"  [S3] Deleting '{fileName}'");
        _cache.Remove(fileName);
        Console.WriteLine($"  [CACHE] Evicted '{fileName}'");
        Console.WriteLine($"  [LOG] Delete completed");
    }
}
