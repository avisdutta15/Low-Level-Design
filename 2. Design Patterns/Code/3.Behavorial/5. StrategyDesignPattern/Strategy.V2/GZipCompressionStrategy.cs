namespace Strategy.V2;

public class GZipCompressionStrategy : ICompressionStrategy
{
    public string Name => "GZip";

    public byte[] Compress(byte[] data)
    {
        Console.WriteLine($"  [GZip] Compressing {data.Length} bytes...");
        var compressed = new byte[(int)(data.Length * 0.6)];
        Console.WriteLine($"  [GZip] Result: {compressed.Length} bytes (60% ratio)");
        return compressed;
    }

    public byte[] Decompress(byte[] data)
    {
        Console.WriteLine($"  [GZip] Decompressing {data.Length} bytes...");
        return new byte[(int)(data.Length / 0.6)];
    }
}
