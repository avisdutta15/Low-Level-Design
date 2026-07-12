namespace Strategy.V1;

/// <summary>
/// Without Strategy: compression algorithm is hardcoded via if/else.
/// 
/// Problems:
///   - Adding a new algorithm (Brotli) = modifying this class
///   - Can't swap algorithm at runtime without conditionals
///   - Violates OCP: not open for extension without modification
///   - Violates SRP: this class handles BOTH storage logic and compression logic
///   - Hard to test: can't test compression in isolation
///   - Algorithm choice logic duplicated wherever compression is needed
/// </summary>
public class FileCompressor
{
    private readonly string _algorithm;

    public FileCompressor(string algorithm)
    {
        _algorithm = algorithm;
    }

    public byte[] Compress(byte[] data)
    {
        if (_algorithm == "gzip")
        {
            Console.WriteLine($"  [GZip] Compressing {data.Length} bytes...");
            // Simulate GZip compression
            var compressed = new byte[(int)(data.Length * 0.6)];
            Console.WriteLine($"  [GZip] Result: {compressed.Length} bytes (60% ratio)");
            return compressed;
        }
        else if (_algorithm == "zip")
        {
            Console.WriteLine($"  [Zip] Compressing {data.Length} bytes...");
            var compressed = new byte[(int)(data.Length * 0.5)];
            Console.WriteLine($"  [Zip] Result: {compressed.Length} bytes (50% ratio)");
            return compressed;
        }
        else if (_algorithm == "lz4")
        {
            Console.WriteLine($"  [LZ4] Compressing {data.Length} bytes (fast mode)...");
            var compressed = new byte[(int)(data.Length * 0.7)];
            Console.WriteLine($"  [LZ4] Result: {compressed.Length} bytes (70% ratio — fast but less compressed)");
            return compressed;
        }
        else if (_algorithm == "none")
        {
            Console.WriteLine($"  [None] No compression — returning original {data.Length} bytes");
            return data;
        }
        else
        {
            throw new ArgumentException($"Unknown compression algorithm: {_algorithm}");
        }
    }

    public byte[] Decompress(byte[] data)
    {
        if (_algorithm == "gzip")
        {
            Console.WriteLine($"  [GZip] Decompressing {data.Length} bytes...");
            return new byte[(int)(data.Length / 0.6)];
        }
        else if (_algorithm == "zip")
        {
            Console.WriteLine($"  [Zip] Decompressing {data.Length} bytes...");
            return new byte[(int)(data.Length / 0.5)];
        }
        else if (_algorithm == "lz4")
        {
            Console.WriteLine($"  [LZ4] Decompressing {data.Length} bytes...");
            return new byte[(int)(data.Length / 0.7)];
        }
        else if (_algorithm == "none")
        {
            return data;
        }
        else
        {
            throw new ArgumentException($"Unknown algorithm: {_algorithm}");
        }
    }
}
