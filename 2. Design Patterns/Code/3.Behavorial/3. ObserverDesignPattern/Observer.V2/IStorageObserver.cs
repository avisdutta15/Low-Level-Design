namespace Observer.V2;

/// <summary>
/// Observer interface — any system that wants to react to storage events
/// implements this. The subject (FileStorageService) only knows this interface.
/// </summary>
public interface IStorageObserver
{
    void OnStorageEvent(StorageEvent storageEvent);
}
