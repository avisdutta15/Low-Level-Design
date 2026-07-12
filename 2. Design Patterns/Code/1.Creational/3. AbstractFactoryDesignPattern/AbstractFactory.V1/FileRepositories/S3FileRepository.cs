namespace AbstractFactory.V1;

public class S3FileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"  [S3] Uploading '{fileName}' to S3 bucket ({content.Length} bytes)");

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"  [S3] Downloading '{fileName}' from S3 bucket");
        return Array.Empty<byte>();
    }

    public void Delete(string fileName)
        => Console.WriteLine($"  [S3] Deleting '{fileName}' from S3 bucket");
}
