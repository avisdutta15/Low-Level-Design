namespace Factory.V1;

public class AzureBlobFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"  [AzureBlob] Uploading '{fileName}' to Blob Storage ({content.Length} bytes)");

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"  [AzureBlob] Downloading '{fileName}' from Blob Storage");
        return Array.Empty<byte>();
    }

    public void Delete(string fileName)
        => Console.WriteLine($"  [AzureBlob] Deleting '{fileName}' from Blob Storage");
}
