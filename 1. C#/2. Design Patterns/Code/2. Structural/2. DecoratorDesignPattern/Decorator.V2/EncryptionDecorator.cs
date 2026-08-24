namespace Decorator.V2;

/// <summary>
/// Decorator: Adds encryption/decryption around ANY IFileRepository.
/// Encrypts on upload, decrypts on download. Inner repo stores encrypted bytes.
/// </summary>
public class EncryptionDecorator : IFileRepository
{
    private readonly IFileRepository _inner;
    private readonly byte _encryptionKey;

    public EncryptionDecorator(IFileRepository inner, byte encryptionKey = 0xAB)
    {
        _inner = inner;
        _encryptionKey = encryptionKey;
    }

    public void Upload(string fileName, byte[] content)
    {
        var encrypted = Encrypt(content);
        Console.WriteLine($"  [ENCRYPT] Encrypted '{fileName}' ({content.Length} -> {encrypted.Length} bytes)");
        _inner.Upload(fileName, encrypted);
    }

    public byte[] Download(string fileName)
    {
        var encrypted = _inner.Download(fileName);
        var decrypted = Decrypt(encrypted);
        Console.WriteLine($"  [ENCRYPT] Decrypted '{fileName}' ({encrypted.Length} -> {decrypted.Length} bytes)");
        return decrypted;
    }

    public void Delete(string fileName)
    {
        _inner.Delete(fileName);
    }

    private byte[] Encrypt(byte[] data)
        => data.Select(b => (byte)(b ^ _encryptionKey)).ToArray();

    private byte[] Decrypt(byte[] data)
        => data.Select(b => (byte)(b ^ _encryptionKey)).ToArray(); // XOR is symmetric
}
