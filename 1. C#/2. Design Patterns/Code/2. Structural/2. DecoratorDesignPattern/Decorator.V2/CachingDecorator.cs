namespace Decorator.V2;

/// <summary>
/// Decorator: Adds in-memory caching around ANY IFileRepository.
/// Cache hits avoid calling the inner repository entirely.
/// </summary>
public class CachingDecorator : IFileRepository
{
    private readonly IFileRepository _inner;
    private readonly Dictionary<string, byte[]> _cache = new();

    public CachingDecorator(IFileRepository inner)
    {
        _inner = inner;
    }

    public void Upload(string fileName, byte[] content)
    {
        _inner.Upload(fileName, content);
        _cache[fileName] = content;
        Console.WriteLine($"  [CACHE] Stored '{fileName}' in cache");
    }

    public byte[] Download(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var cached))
        {
            Console.WriteLine($"  [CACHE] Hit for '{fileName}' ({cached.Length} bytes)");
            return cached;
        }

        Console.WriteLine($"  [CACHE] Miss for '{fileName}' - fetching from storage");
        var data = _inner.Download(fileName);
        _cache[fileName] = data;
        return data;
    }

    public void Delete(string fileName)
    {
        _inner.Delete(fileName);
        _cache.Remove(fileName);
        Console.WriteLine($"  [CACHE] Evicted '{fileName}' from cache");
    }
}
