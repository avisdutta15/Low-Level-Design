namespace Mediator.V2;

/// <summary>
/// THE MEDIATOR — contains all coordination logic between components.
/// 
/// Each component only knows the Mediator interface.
/// The Mediator knows all components and decides who reacts to what.
/// All business rules about cross-component interaction live HERE.
/// </summary>
public class StorageMediator : IStorageMediator
{
    public FileStorageComponent Storage { get; }
    public QuotaComponent Quota { get; }
    public SearchIndexComponent Search { get; }
    public NotificationComponent Notification { get; }

    public StorageMediator(long maxQuotaBytes = 5000)
    {
        // Mediator creates and owns all components
        Storage = new FileStorageComponent(this);
        Quota = new QuotaComponent(this, maxQuotaBytes);
        Search = new SearchIndexComponent(this);
        Notification = new NotificationComponent(this);
    }

    /// <summary>
    /// Central coordination point — all cross-component logic lives here.
    /// Components never call each other — they notify the mediator,
    /// and the mediator decides what to do.
    /// </summary>
    public void Notify(object sender, string eventType, Dictionary<string, object>? data = null)
    {
        switch (eventType)
        {
            case "FileUploaded":
                var fileName = (string)data!["fileName"];
                var author = (string)data["author"];
                var sizeBytes = (long)data["sizeBytes"];

                // Coordination: update quota, index, notify
                Quota.ConsumeSpace(sizeBytes);
                Search.IndexFile(fileName, author);
                Notification.SendAlert($"'{fileName}' uploaded by {author}");
                break;

            case "FileDeleted":
                var deletedFile = (string)data!["fileName"];
                var deletedSize = (long)data["sizeBytes"];

                // Coordination: release quota, remove from index, notify
                Quota.ReleaseSpace(deletedSize);
                Search.RemoveFile(deletedFile);
                Notification.SendAlert($"'{deletedFile}' deleted");
                break;

            case "QuotaWarning":
                var used = (long)data!["usedBytes"];
                var max = (long)data["maxBytes"];
                Notification.SendAlert($"WARNING: Storage at {used * 100 / max}% capacity!");
                break;

            case "QuotaExceeded":
                // Bidirectional: Quota tells Mediator → Mediator tells Storage to pause
                Storage.PauseUploads();
                Notification.SendAlert("CRITICAL: Quota exceeded — uploads paused!");
                break;

            case "QuotaAvailable":
                // Bidirectional: Quota freed up → Mediator tells Storage to resume
                Storage.ResumeUploads();
                Notification.SendAlert("Quota available — uploads resumed");
                break;
        }
    }
}
