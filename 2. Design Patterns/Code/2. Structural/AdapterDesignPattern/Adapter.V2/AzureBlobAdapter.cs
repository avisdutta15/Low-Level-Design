namespace Adapter.V2;

/// <summary>
/// THE ADAPTER — bridges the gap between IFileRepository and ThirdPartyAzureBlobClient.
/// 
/// It implements our interface (IFileRepository) and internally delegates
/// to the third-party Azure client, translating:
///   - Method names (Upload → PutBlob, Download → GetBlob, etc.)
///   - Parameter types (byte[] → Stream, adds containerName)
///   - Return types (Stream → byte[])
/// 
/// The client (DocumentService) sees only IFileRepository.
/// The adapter hides all Azure-specific complexity.
/// </summary>
public class AzureBlobAdapter : IFileRepository
{
    private readonly ThirdPartyAzureBlobClient _azureClient;
    private readonly string _containerName;

    public AzureBlobAdapter(ThirdPartyAzureBlobClient azureClient, string containerName)
    {
        _azureClient = azureClient;
        _containerName = containerName;
    }

    public void Upload(string fileName, byte[] content)
    {
        // Translate: byte[] → Stream, add containerName
        using var stream = new MemoryStream(content);
        _azureClient.PutBlob(_containerName, fileName, stream);
    }

    public byte[] Download(string fileName)
    {
        // Translate: Stream → byte[], inject containerName
        using var stream = _azureClient.GetBlob(_containerName, fileName);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public void Delete(string fileName)
    {
        // Translate: Delete → RemoveBlob, inject containerName
        _azureClient.RemoveBlob(_containerName, fileName);
    }

    public bool Exists(string fileName)
    {
        // Translate: Exists → BlobExists, inject containerName
        return _azureClient.BlobExists(_containerName, fileName);
    }
}
