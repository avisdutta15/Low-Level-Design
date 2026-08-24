using System.Diagnostics;

namespace Decorator.V1;

/// <summary>
/// Approach 1: Subclass for each cross-cutting concern.
/// 
/// Problem: For N features (logging, caching, encryption, retry, metrics)
/// and M storage providers (S3, Local, Azure), we need N x M classes!
///   - S3FileRepositoryWithLogging
///   - S3FileRepositoryWithCaching
///   - S3FileRepositoryWithLoggingAndCaching
///   - LocalFileRepositoryWithLogging
///   - LocalFileRepositoryWithCaching
///   - ... CLASS EXPLOSION!
/// </summary>
public class S3FileRepositoryWithLogging : IFileRepository
{
    public void Upload(string fileName, byte[] content)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  [LOG] Upload started: '{fileName}' ({content.Length} bytes)");
        Console.WriteLine($"  [S3] Uploading '{fileName}' ({content.Length} bytes)");
        Console.WriteLine($"  [LOG] Upload completed in {sw.ElapsedMilliseconds}ms");
    }

    public byte[] Download(string fileName)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  [LOG] Download started: '{fileName}'");
        Console.WriteLine($"  [S3] Downloading '{fileName}'");
        Console.WriteLine($"  [LOG] Download completed in {sw.ElapsedMilliseconds}ms");
        return new byte[] { 1, 2, 3 };
    }

    public void Delete(string fileName)
    {
        Console.WriteLine($"  [LOG] Delete started: '{fileName}'");
        Console.WriteLine($"  [S3] Deleting '{fileName}'");
        Console.WriteLine($"  [LOG] Delete completed");
    }
}
