namespace Strategy.V2;

/// <summary>
/// Context — uses a compression strategy without knowing which algorithm it is.
/// Strategy can be set at construction or swapped at runtime.
/// </summary>
public class FileStorageService
{
    private ICompressionStrategy _compressionStrategy;

    public FileStorageService(ICompressionStrategy compressionStrategy)
    {
        _compressionStrategy = compressionStrategy;
    }

    /// <summary>
    /// Swap the algorithm at runtime — no class modification needed.
    /// </summary>
    public void SetCompressionStrategy(ICompressionStrategy strategy)
    {
        Console.WriteLine($"  [Storage] Switching compression: {_compressionStrategy.Name} → {strategy.Name}");
        _compressionStrategy = strategy;
    }

    public void Upload(string fileName, byte[] content)
    {
        Console.WriteLine($"  [Storage] Preparing '{fileName}' for upload...");

        // Delegate compression to the strategy — doesn't know which algorithm
        var compressed = _compressionStrategy.Compress(content);

        Console.WriteLine($"  [Storage] Uploading '{fileName}' ({compressed.Length} bytes compressed)");
    }

    public byte[] Download(string fileName, byte[] compressedData)
    {
        Console.WriteLine($"  [Storage] Downloading '{fileName}'...");

        // Delegate decompression to the strategy
        var decompressed = _compressionStrategy.Decompress(compressedData);

        Console.WriteLine($"  [Storage] Decompressed to {decompressed.Length} bytes");
        return decompressed;
    }
}
