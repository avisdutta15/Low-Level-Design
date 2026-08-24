namespace Observer.V2;

public class SearchIndexObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
    {
        if (e.EventType == "Uploaded")
            Console.WriteLine($"  [Search] Indexed '{e.FileName}' by {e.Author}");
        else if (e.EventType == "Deleted")
            Console.WriteLine($"  [Search] Removed '{e.FileName}' from index");
        // Ignore other events
    }
}
