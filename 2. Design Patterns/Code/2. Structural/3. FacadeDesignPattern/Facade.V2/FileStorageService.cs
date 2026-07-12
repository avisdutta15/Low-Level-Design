namespace Facade.V2;

public class FileStorageService
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"  [FileStorage] Uploading '{fileName}' ({content.Length} bytes)");

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"  [FileStorage] Downloading '{fileName}'");
        return new byte[] { 1, 2, 3 };
    }

    public void Delete(string fileName)
        => Console.WriteLine($"  [FileStorage] Deleting '{fileName}'");
}
