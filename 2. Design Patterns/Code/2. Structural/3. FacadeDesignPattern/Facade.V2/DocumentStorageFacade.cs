namespace Facade.V2;

/// <summary>
/// THE FACADE — provides a simple, unified interface to the complex
/// document storage subsystem.
/// 
/// It orchestrates 5 services (FileStorage, Metadata, Search, VirusScan,
/// Notification) behind simple methods like Upload() and Delete().
/// 
/// The client calls ONE method. The Facade handles:
///   - Correct ordering of operations
///   - Error handling and rollback
///   - Coordination between subsystems
///   - All the complex details hidden from the client
/// </summary>
public class DocumentStorageFacade
{
    private readonly FileStorageService _fileStorage;
    private readonly MetadataService _metadata;
    private readonly SearchIndexService _search;
    private readonly VirusScanService _virusScan;
    private readonly NotificationService _notification;

    public DocumentStorageFacade(
        FileStorageService fileStorage,
        MetadataService metadata,
        SearchIndexService search,
        VirusScanService virusScan,
        NotificationService notification)
    {
        _fileStorage = fileStorage;
        _metadata = metadata;
        _search = search;
        _virusScan = virusScan;
        _notification = notification;
    }

    /// <summary>
    /// Simple method — hides the 5-step orchestration from the client.
    /// </summary>
    public bool UploadDocument(string fileName, byte[] content, string author, string contentType = "application/octet-stream")
    {
        // 1. Virus scan (before anything else)
        if (!_virusScan.Scan(content))
        {
            Console.WriteLine("  [Facade] REJECTED: File failed virus scan");
            return false;
        }

        // 2. Store the file
        _fileStorage.Upload(fileName, content);

        // 3. Save metadata
        _metadata.SaveMetadata(fileName, author, content.Length, contentType);

        // 4. Index for search
        var metadataDict = new Dictionary<string, string>
        {
            ["author"] = author,
            ["contentType"] = contentType
        };
        _search.IndexDocument(fileName, fileName, metadataDict);

        // 5. Notify subscribers
        _notification.NotifyUpload(fileName, author);

        return true;
    }

    /// <summary>
    /// Simple delete — coordinates removal across all subsystems.
    /// </summary>
    public void DeleteDocument(string fileName)
    {
        _fileStorage.Delete(fileName);
        _metadata.DeleteMetadata(fileName);
        _search.RemoveFromIndex(fileName);
        _notification.NotifyDeletion(fileName);
    }

    /// <summary>
    /// Simple download — just the file content.
    /// </summary>
    public byte[] DownloadDocument(string fileName)
    {
        return _fileStorage.Download(fileName);
    }

    /// <summary>
    /// Simple search — hides the search subsystem complexity.
    /// </summary>
    public List<string> SearchDocuments(string query)
    {
        return _search.Search(query);
    }
}
