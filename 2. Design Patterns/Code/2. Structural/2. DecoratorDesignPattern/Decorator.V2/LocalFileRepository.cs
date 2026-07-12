namespace Decorator.V2;

/// <summary>
/// Another concrete component — decorators work with ANY IFileRepository.
/// </summary>
public class LocalFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"  [Local] Writing '{fileName}' ({content.Length} bytes)");

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"  [Local] Reading '{fileName}'");
        return new byte[] { 4, 5, 6 };
    }

    public void Delete(string fileName)
        => Console.WriteLine($"  [Local] Deleting '{fileName}'");
}
