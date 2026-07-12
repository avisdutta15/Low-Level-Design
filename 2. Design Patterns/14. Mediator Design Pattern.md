# Mediator Design Pattern

## Table of Contents

- [What is the Mediator Pattern?](#what-is-the-mediator-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Mediator?](#v1--why-do-we-need-mediator)
- [V2 — How to Implement Mediator](#v2--how-to-implement-mediator)
- [Observer vs Mediator](#observer-vs-mediator)
- [When to Use Mediator](#when-to-use-mediator)

---

## What is the Mediator Pattern?

The Mediator pattern is a **behavioral design pattern** that reduces chaotic N×N dependencies between components by introducing a central hub (mediator) that handles all communication. Components never call each other directly — they notify the mediator, and the mediator coordinates the response.

**Core Idea:**
- Components only know the Mediator interface — not each other
- All cross-component coordination logic lives in ONE place (the Mediator)
- Components communicate indirectly: Component → Mediator → Other Components
- Reduces N×N dependencies to N×1 (star topology)

**Key Distinction:**
- Without Mediator: Storage→Quota, Storage→Search, Storage→Notification, Quota→Notification (mesh)
- With Mediator: Storage→Mediator, Quota→Mediator, Search→Mediator (star)

---

## UML Diagram

```
                    ┌─────────────────────────────────────────────┐
                    │        «interface» IStorageMediator          │
                    ├─────────────────────────────────────────────┤
                    │ + Notify(sender, eventType, data?)           │
                    └──────────────────┬──────────────────────────┘
                                       │ implements
                                       ▼
                    ┌─────────────────────────────────────────────┐
                    │            StorageMediator                   │
                    │          (THE MEDIATOR)                      │
                    ├─────────────────────────────────────────────┤
                    │ + Storage: FileStorageComponent              │
                    │ + Quota: QuotaComponent                     │
                    │ + Search: SearchIndexComponent               │
                    │ + Notification: NotificationComponent        │
                    ├─────────────────────────────────────────────┤
                    │ + Notify(sender, eventType, data?)           │
                    │   → routes events to appropriate components  │
                    │   → contains ALL coordination logic          │
                    └────┬──────────┬──────────┬─────────┬────────┘
                         │          │          │         │
            knows/calls  │          │          │         │  knows/calls
                         ▼          ▼          ▼         ▼
              ┌──────────────┐ ┌─────────┐ ┌────────┐ ┌────────────┐
              │FileStorage   │ │  Quota  │ │ Search │ │Notification│
              │  Component   │ │Component│ │  Index │ │ Component  │
              ├──────────────┤ ├─────────┤ ├────────┤ ├────────────┤
              │+Upload()     │ │+HasSpace│ │+Index()│ │+SendAlert()│
              │+Delete()     │ │+Consume │ │+Remove │ │            │
              │+PauseUploads │ │+Release │ │        │ │            │
              │+ResumeUploads│ │         │ │        │ │            │
              └──────┬───────┘ └────┬────┘ └────────┘ └────────────┘
                     │              │
                     │              │ notifies mediator
                     │              ▼
                     │     "QuotaExceeded"
                     │         │
                     │         ▼ (mediator routes)
                     │   Storage.PauseUploads()
                     │
                     └─── notifies mediator → "FileUploaded"
                                │
                                ▼ (mediator coordinates)
                          Quota.ConsumeSpace()
                          Search.IndexFile()
                          Notification.SendAlert()

WITHOUT Mediator (mesh — N×N):          WITH Mediator (star — N×1):
  Storage ←→ Quota                        Storage → Mediator
  Storage ←→ Search                       Quota → Mediator
  Storage ←→ Notification                 Search → Mediator
  Quota → Notification                    Notification → Mediator
  (6 connections)                         (4 connections, 1 hub)
```

---

## V1 — Why Do We Need Mediator?

**Scenario:** A storage system has 4 components that need to interact:
- **FileStorage** — stores/deletes files
- **Quota** — tracks space, warns at 90%, blocks at 100%
- **SearchIndex** — indexes/removes files
- **Notification** — sends alerts

**Without Mediator — components call each other directly:**

```csharp
public class FileStorageService
{
    private readonly QuotaService _quota;         // knows Quota
    private readonly SearchIndexService _search;  // knows Search
    private readonly NotificationService _notification; // knows Notification

    public bool Upload(string fileName, byte[] content, string author)
    {
        if (!_quota.HasSpace(content.Length))
        {
            _notification.SendAlert("Quota exceeded");
            return false;
        }
        // store file...
        _quota.ConsumeSpace(content.Length);
        _search.IndexFile(fileName, author);
        _notification.SendAlert($"'{fileName}' uploaded");
        return true;
    }
}

public class QuotaService
{
    private readonly NotificationService _notification; // knows Notification

    public void ConsumeSpace(long bytes)
    {
        _usedBytes += bytes;
        if (_usedBytes > _maxBytes * 0.9)
            _notification.SendAlert("WARNING: Quota > 90%!");
    }
}
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| N×N coupling | Storage→Quota, Storage→Search, Storage→Notification, Quota→Notification |
| Circular risk | Quota→Notification→Storage→Quota → infinite loop potential |
| OCP violation | Adding CacheInvalidation = modifying Storage + possibly Quota |
| Scattered logic | "What happens on upload?" spread across Storage, Quota, Notification |
| Hard to test | Must mock 3 services to test Storage in isolation |
| Bidirectional complexity | Quota should pause Storage uploads — but Storage owns Quota dependency, not the other way |

---

## V2 — How to Implement Mediator

**Step 1: Define the Mediator interface**

```csharp
public interface IStorageMediator
{
    void Notify(object sender, string eventType, Dictionary<string, object>? data = null);
}
```

**Step 2: Base Component (knows only the Mediator)**

```csharp
public abstract class BaseComponent
{
    protected IStorageMediator Mediator { get; }
    protected BaseComponent(IStorageMediator mediator) => Mediator = mediator;
}
```

**Step 3: Components (no cross-references)**

```csharp
public class FileStorageComponent : BaseComponent
{
    public FileStorageComponent(IStorageMediator mediator) : base(mediator) { }

    public void Upload(string fileName, byte[] content, string author)
    {
        Console.WriteLine($"[Storage] Uploading '{fileName}'");
        // Tell mediator — let IT coordinate
        Mediator.Notify(this, "FileUploaded", new Dictionary<string, object>
        {
            ["fileName"] = fileName, ["author"] = author, ["sizeBytes"] = (long)content.Length
        });
    }

    public void Delete(string fileName, long fileSize)
    {
        Console.WriteLine($"[Storage] Deleting '{fileName}'");
        Mediator.Notify(this, "FileDeleted", new Dictionary<string, object>
        {
            ["fileName"] = fileName, ["sizeBytes"] = fileSize
        });
    }

    public void PauseUploads() => Console.WriteLine("[Storage] Uploads PAUSED");
    public void ResumeUploads() => Console.WriteLine("[Storage] Uploads RESUMED");
}

public class QuotaComponent : BaseComponent
{
    private long _usedBytes;
    private readonly long _maxBytes;

    public QuotaComponent(IStorageMediator mediator, long maxBytes) : base(mediator)
        => _maxBytes = maxBytes;

    public bool HasSpace(long bytes) => (_usedBytes + bytes) <= _maxBytes;

    public void ConsumeSpace(long bytes)
    {
        _usedBytes += bytes;
        if (_usedBytes > _maxBytes * 0.9)
            Mediator.Notify(this, "QuotaWarning", ...);
        if (_usedBytes >= _maxBytes)
            Mediator.Notify(this, "QuotaExceeded");
    }

    public void ReleaseSpace(long bytes)
    {
        bool wasExceeded = _usedBytes >= _maxBytes;
        _usedBytes -= bytes;
        if (wasExceeded && _usedBytes < _maxBytes)
            Mediator.Notify(this, "QuotaAvailable");
    }
}
```

**Step 4: The Mediator (all coordination logic centralized)**

```csharp
public class StorageMediator : IStorageMediator
{
    public FileStorageComponent Storage { get; }
    public QuotaComponent Quota { get; }
    public SearchIndexComponent Search { get; }
    public NotificationComponent Notification { get; }

    public StorageMediator(long maxQuotaBytes)
    {
        Storage = new FileStorageComponent(this);
        Quota = new QuotaComponent(this, maxQuotaBytes);
        Search = new SearchIndexComponent(this);
        Notification = new NotificationComponent(this);
    }

    public void Notify(object sender, string eventType, Dictionary<string, object>? data = null)
    {
        switch (eventType)
        {
            case "FileUploaded":
                Quota.ConsumeSpace((long)data!["sizeBytes"]);
                Search.IndexFile((string)data["fileName"], (string)data["author"]);
                Notification.SendAlert($"'{data["fileName"]}' uploaded");
                break;

            case "FileDeleted":
                Quota.ReleaseSpace((long)data!["sizeBytes"]);
                Search.RemoveFile((string)data["fileName"]);
                Notification.SendAlert($"'{data["fileName"]}' deleted");
                break;

            case "QuotaExceeded":
                Storage.PauseUploads();  // Bidirectional! Quota→Mediator→Storage
                Notification.SendAlert("Quota exceeded — uploads paused!");
                break;

            case "QuotaAvailable":
                Storage.ResumeUploads();
                Notification.SendAlert("Quota available — uploads resumed");
                break;
        }
    }
}
```

**Step 5: Usage**

```csharp
var mediator = new StorageMediator(maxQuotaBytes: 5000);

mediator.Storage.Upload("report.pdf", new byte[1000], "Alice");
// → Mediator coordinates: Quota.Consume + Search.Index + Notification.Alert

mediator.Storage.Upload("huge.zip", new byte[4500], "Bob");
// → Quota exceeds → Mediator calls Storage.PauseUploads()

mediator.Storage.Delete("report.pdf", 1000);
// → Quota freed → Mediator calls Storage.ResumeUploads()
```

---

## Observer vs Mediator

| Aspect | Observer | Mediator |
|--------|----------|----------|
| Direction | One-to-many broadcast | Many-to-many through a hub |
| Who knows whom | Subject → IObserver (loose) | Components → IMediator (centralized) |
| Communication | Broadcast — all observers get same event | Routed — Mediator decides who gets what |
| Logic location | Each observer has its own reaction logic | Mediator contains all coordination logic |
| Bidirectional? | No — Subject notifies, observers react | Yes — Mediator can call back to sender |
| Adding a participant | New observer class (Subject unchanged) | Mediator may need update |
| Coupling shape | Fan-out (1 → N) | Star (N → 1 → N) |

**Visual:**

```
OBSERVER (fan-out, independent reactions):
  FileStorage.Upload()
    → notify LoggingObserver       (logs independently)
    → notify SearchObserver        (indexes independently)
    → notify NotificationObserver  (alerts independently)
    → notify MetricsObserver       (counts independently)
  
  Observers don't affect each other or the subject.

MEDIATOR (star, coordinated reactions):
  FileStorage.Upload()
    → Mediator.Notify("FileUploaded")
        → Quota.ConsumeSpace()
            → if exceeded → Mediator.Notify("QuotaExceeded")
                → Storage.PauseUploads()  ← BIDIRECTIONAL
                → Notification.SendAlert()
        → Search.IndexFile()
        → Notification.SendAlert()
  
  Mediator coordinates — components affect each other through the hub.
```

**When to use which:**

| Scenario | Pattern |
|----------|---------|
| "File uploaded — log it, index it, count it" (independent reactions) | Observer |
| "File uploaded — check quota, if exceeded pause uploads, notify admin" (coordinated) | Mediator |
| Subscribers are independent and don't affect each other | Observer |
| Components need bidirectional communication | Mediator |
| Adding reactions without touching publisher | Observer |
| Centralizing complex interaction rules | Mediator |

---

## When to Use Mediator

### Use Mediator When:

| Scenario | Why Mediator Helps |
|----------|-------------------|
| Components have many-to-many dependencies | Reduces N×N to N×1 |
| Components need bidirectional communication | Mediator routes both ways |
| Coordination logic is complex and scattered | Centralizes in one class |
| You need to prevent circular dependencies | Star topology has no cycles |
| Adding new coordination rules frequently | Single place to modify |

### Don't Use Mediator When:

| Scenario | Why Not |
|----------|---------|
| Reactions are independent (no coordination needed) | Use Observer — simpler |
| Only one component needs to react | Direct call is fine |
| Mediator becomes a God object with too much logic | Split into multiple mediators or use CQRS |
| Communication is always one-way (broadcast) | Observer is the right fit |

### Real-World .NET Examples:

| Example | What It Mediates |
|---------|-----------------|
| MediatR library | Commands/Queries → Handlers (request routing) |
| ASP.NET Core `IMediator` | Controller → Service layer decoupling |
| Chat room | Users ↔ ChatRoom ↔ Users (no direct user-to-user) |
| Air traffic control | Planes ↔ Tower ↔ Planes (planes don't talk to each other) |
| UI form validation | Fields ↔ FormMediator ↔ SubmitButton (field changes affect button state) |
| Event sourcing | Commands ↔ EventStore ↔ Projections |

### Mediator Anti-pattern: God Mediator

If your Mediator handles 50 event types with hundreds of lines of coordination logic, it's become a God class. Solutions:
- Split into domain-specific mediators (UploadMediator, QuotaMediator)
- Use CQRS (separate command and query mediators)
- Use a pipeline pattern inside the mediator
