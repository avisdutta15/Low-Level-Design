namespace Observer.V2;

/// <summary>
/// The event data passed to observers when something happens in storage.
/// </summary>
public class StorageEvent
{
    public string EventType { get; }   // "Uploaded", "Deleted", "Downloaded"
    public string FileName { get; }
    public string Author { get; }
    public DateTime Timestamp { get; }
    public long FileSizeBytes { get; }

    public StorageEvent(string eventType, string fileName, string author, long fileSizeBytes = 0)
    {
        EventType = eventType;
        FileName = fileName;
        Author = author;
        Timestamp = DateTime.UtcNow;
        FileSizeBytes = fileSizeBytes;
    }
}
