namespace Strategy.V2;

/// <summary>
/// Strategy interface — defines the contract for all compression algorithms.
/// Each algorithm is a separate class implementing this interface.
/// </summary>
public interface ICompressionStrategy
{
    string Name { get; }
    byte[] Compress(byte[] data);
    byte[] Decompress(byte[] data);
}
