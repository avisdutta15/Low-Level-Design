namespace Strategy.V2;

public class LZ4CompressionStrategy : ICompressionStrategy
{
    public string Name => "LZ4";

    public byte[] Compress(byte[] data)
    {
        Console.WriteLine($"  [LZ4] Compressing {data.Length} bytes (fast mode)...");
        var compressed = new byte[(int)(data.Length * 0.7)];
        Console.WriteLine($"  [LZ4] Result: {compressed.Length} bytes (70% — fast, less compressed)");
        return compressed;
    }

    public byte[] Decompress(byte[] data)
    {
        Console.WriteLine($"  [LZ4] Decompressing {data.Length} bytes...");
        return new byte[(int)(data.Length / 0.7)];
    }
}
