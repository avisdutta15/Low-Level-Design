namespace Adapter.V1;

/// <summary>
/// A third-party Azure Blob Storage client (e.g., from a NuGet package).
/// 
/// WE DON'T OWN THIS CODE — we can't modify it.
/// 
/// Problem: It has a COMPLETELY DIFFERENT interface than our IFileRepository.
///   - Different method names (PutBlob vs Upload, GetBlob vs Download)
///   - Different parameter types (Stream vs byte[], BlobPath vs string)
///   - Different return types (BlobResult vs byte[])
///   - Extra parameters we don't use (containerName, options)
/// </summary>
public class ThirdPartyAzureBlobClient
{
    public void PutBlob(string containerName, string blobPath, Stream content, string contentType = "application/octet-stream")
    {
        Console.WriteLine($"  [AzureSDK] PutBlob → container='{containerName}', blob='{blobPath}', size={content.Length}, type='{contentType}'");
    }

    public Stream GetBlob(string containerName, string blobPath)
    {
        Console.WriteLine($"  [AzureSDK] GetBlob → container='{containerName}', blob='{blobPath}'");
        return new MemoryStream(new byte[] { 10, 20, 30 });
    }

    public void RemoveBlob(string containerName, string blobPath)
    {
        Console.WriteLine($"  [AzureSDK] RemoveBlob → container='{containerName}', blob='{blobPath}'");
    }

    public bool BlobExists(string containerName, string blobPath)
    {
        Console.WriteLine($"  [AzureSDK] BlobExists → container='{containerName}', blob='{blobPath}'");
        return true;
    }
}
