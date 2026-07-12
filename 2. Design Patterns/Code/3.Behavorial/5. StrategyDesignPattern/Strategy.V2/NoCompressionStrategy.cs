namespace Strategy.V2;

public class NoCompressionStrategy : ICompressionStrategy
{
    public string Name => "None";

    public byte[] Compress(byte[] data)
    {
        Console.WriteLine($"  [None] No compression — {data.Length} bytes unchanged");
        return data;
    }

    public byte[] Decompress(byte[] data) => data;
}
