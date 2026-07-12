namespace Adapter.V2;

/// <summary>
/// Existing implementation — already conforms to IFileRepository.
/// No adapter needed for this one.
/// </summary>
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

    public bool Exists(string fileName)
    {
        Console.WriteLine($"  [S3] Checking if '{fileName}' exists");
        return true;
    }
}
