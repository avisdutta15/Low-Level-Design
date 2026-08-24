namespace Strategy.V2;

public class ZipCompressionStrategy : ICompressionStrategy
{
    public string Name => "Zip";

    public byte[] Compress(byte[] data)
    {
        Console.WriteLine($"  [Zip] Compressing {data.Length} bytes...");
        var compressed = new byte[(int)(data.Length * 0.5)];
        Console.WriteLine($"  [Zip] Result: {compressed.Length} bytes (50% ratio)");
        return compressed;
    }

    public byte[] Decompress(byte[] data)
    {
        Console.WriteLine($"  [Zip] Decompressing {data.Length} bytes...");
        return new byte[(int)(data.Length / 0.5)];
    }
}
