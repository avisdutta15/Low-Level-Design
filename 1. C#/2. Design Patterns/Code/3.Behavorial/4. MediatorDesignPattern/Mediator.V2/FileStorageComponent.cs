namespace Mediator.V2;

/// <summary>
/// Component: File Storage — stores/deletes files.
/// Does NOT know about Quota, Search, or Notification.
/// Communicates only through the Mediator.
/// </summary>
public class FileStorageComponent : BaseComponent
{
    public FileStorageComponent(IStorageMediator mediator) : base(mediator) { }

    public void Upload(string fileName, byte[] content, string author)
    {
        Console.WriteLine($"  [Storage] Uploading '{fileName}' ({content.Length} bytes)");

        // Tell the mediator — let it coordinate with other components
        Mediator.Notify(this, "FileUploaded", new Dictionary<string, object>
        {
            ["fileName"] = fileName,
            ["author"] = author,
            ["sizeBytes"] = (long)content.Length
        });
    }

    public void Delete(string fileName, long fileSize)
    {
        Console.WriteLine($"  [Storage] Deleting '{fileName}'");

        Mediator.Notify(this, "FileDeleted", new Dictionary<string, object>
        {
            ["fileName"] = fileName,
            ["sizeBytes"] = fileSize
        });
    }

    public void PauseUploads()
    {
        Console.WriteLine($"  [Storage] ⚠️ Uploads PAUSED — quota exceeded");
    }

    public void ResumeUploads()
    {
        Console.WriteLine($"  [Storage] ✓ Uploads RESUMED — quota available");
    }
}
