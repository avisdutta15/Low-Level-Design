namespace Decorator.V1;

public class S3FileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"  [S3] Uploading '{fileName}' ({content.Length} bytes)");

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"  [S3] Downloading '{fileName}'");
        return new byte[] { 1, 2, 3 };
    }

    public void Delete(string fileName)
        => Console.WriteLine($"  [S3] Deleting '{fileName}'");
}
