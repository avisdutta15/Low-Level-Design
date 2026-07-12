namespace Adapter.V2;

/// <summary>
/// Adaptee — the third-party class with an incompatible interface.
/// We don't own this code. We can't modify it.
/// </summary>
public class ThirdPartyAzureBlobClient
{
    public void PutBlob(string containerName, string blobPath, Stream content, string contentType = "application/octet-stream")
    {
        Console.WriteLine($"  [AzureSDK] PutBlob -> container='{containerName}', blob='{blobPath}', size={content.Length}");
    }

    public Stream GetBlob(string containerName, string blobPath)
    {
        Console.WriteLine($"  [AzureSDK] GetBlob -> container='{containerName}', blob='{blobPath}'");
        return new MemoryStream(new byte[] { 10, 20, 30 });
    }

    public void RemoveBlob(string containerName, string blobPath)
    {
        Console.WriteLine($"  [AzureSDK] RemoveBlob -> container='{containerName}', blob='{blobPath}'");
    }

    public bool BlobExists(string containerName, string blobPath)
    {
        Console.WriteLine($"  [AzureSDK] BlobExists -> container='{containerName}', blob='{blobPath}'");
        return true;
    }
}
