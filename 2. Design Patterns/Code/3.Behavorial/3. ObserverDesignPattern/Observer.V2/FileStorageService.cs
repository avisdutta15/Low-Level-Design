namespace Observer.V2;

/// <summary>
/// Subject (Publisher) — performs storage operations and notifies observers.
/// 
/// Key: This class has ZERO knowledge of Logger, SearchIndex, Notification, etc.
/// It only knows about IStorageObserver. Observers subscribe at runtime.
/// </summary>
public class FileStorageService : IStorageSubject
{
    private readonly List<IStorageObserver> _observers = new();

    public void Subscribe(IStorageObserver observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }

    public void Unsubscribe(IStorageObserver observer)
    {
        _observers.Remove(observer);
    }

    private void NotifyAll(StorageEvent storageEvent)
    {
        foreach (var observer in _observers)
        {
            observer.OnStorageEvent(storageEvent);
        }
    }

    public void Upload(string fileName, byte[] content, string author)
    {
        // Core responsibility ONLY: store the file
        Console.WriteLine($"  [Storage] Uploading '{fileName}' ({content.Length} bytes)");

        // Notify all observers — storage service doesn't know who they are
        NotifyAll(new StorageEvent("Uploaded", fileName, author, content.Length));
    }

    public void Delete(string fileName, string author)
    {
        Console.WriteLine($"  [Storage] Deleting '{fileName}'");
        NotifyAll(new StorageEvent("Deleted", fileName, author));
    }

    public void Download(string fileName, string author)
    {
        Console.WriteLine($"  [Storage] Downloading '{fileName}'");
        NotifyAll(new StorageEvent("Downloaded", fileName, author));
    }
}
