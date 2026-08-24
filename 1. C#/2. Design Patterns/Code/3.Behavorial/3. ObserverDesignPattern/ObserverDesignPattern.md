# Observer Design Pattern

## Table of Contents

- [What is the Observer Pattern?](#what-is-the-observer-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Observer?](#v1--why-do-we-need-observer)
- [V2 — How to Implement Observer](#v2--how-to-implement-observer)
- [When to Use Observer](#when-to-use-observer)

---

## What is the Observer Pattern?

The Observer pattern is a **behavioral design pattern** that defines a one-to-many dependency between objects. When the **Subject** (publisher) changes state, all its **Observers** (subscribers) are notified and updated automatically.

**Core Idea:**
- The Subject maintains a list of observers and notifies them when events occur
- Observers subscribe/unsubscribe at runtime — no hardcoded dependencies
- The Subject only knows the `IObserver` interface — not concrete implementations
- Adding a new observer = new class implementing the interface, no changes to the Subject

**Key Distinction:**
- Without Observer: Subject calls `_logger.Log()`, `_search.Index()`, `_notification.Send()` directly — knows everyone
- With Observer: Subject calls `NotifyAll(event)` — doesn't know or care who's listening

---

## UML Diagram

```
┌──────────────────────────────────────────────┐
│         «interface» IStorageSubject           │
├──────────────────────────────────────────────┤
│ + Subscribe(observer: IStorageObserver)       │
│ + Unsubscribe(observer: IStorageObserver)     │
└──────────────────┬───────────────────────────┘
                   │ implements
                   ▼
┌──────────────────────────────────────────────┐
│           FileStorageService                  │
│              (Subject)                         │
├──────────────────────────────────────────────┤
│ - _observers: List<IStorageObserver>          │
├──────────────────────────────────────────────┤
│ + Subscribe(observer)                         │
│ + Unsubscribe(observer)                       │
│ - NotifyAll(event: StorageEvent)              │
│ + Upload(fileName, content, author)           │
│ + Delete(fileName, author)                    │
│ + Download(fileName, author)                  │
└──────────────────┬───────────────────────────┘
                   │ notifies
                   ▼
┌──────────────────────────────────────────────┐
│        «interface» IStorageObserver           │
├──────────────────────────────────────────────┤
│ + OnStorageEvent(event: StorageEvent)         │
└──────────────────┬───────────────────────────┘
                   │ implements
     ┌─────────────┼──────────────┬──────────────┬──────────────┐
     │             │              │              │              │
     ▼             ▼              ▼              ▼              ▼
┌─────────┐ ┌──────────┐ ┌────────────┐ ┌──────────┐ ┌─────────┐
│ Logging │ │  Search  │ │Notification│ │  Audit   │ │ Metrics │
│Observer │ │  Index   │ │  Observer  │ │  Trail   │ │Observer │
│         │ │ Observer │ │            │ │ Observer │ │         │
└─────────┘ └──────────┘ └────────────┘ └──────────┘ └─────────┘

Event flow:
  storage.Upload("file.pdf", content, "Alice")
    → StorageEvent { Type="Uploaded", FileName="file.pdf", Author="Alice" }
    → NotifyAll()
        → LoggingObserver.OnStorageEvent(event)
        → SearchIndexObserver.OnStorageEvent(event)
        → NotificationObserver.OnStorageEvent(event)
        → AuditTrailObserver.OnStorageEvent(event)
        → MetricsObserver.OnStorageEvent(event)
```

---

## V1 — Why Do We Need Observer?

**Scenario:** When a file is uploaded/deleted, multiple systems need to react — logger, search index, notification service, audit trail.

**Without Observer — Subject directly calls every dependent system:**

```csharp
public class FileStorageService
{
    private readonly LoggingService _logger;
    private readonly SearchIndexService _search;
    private readonly NotificationService _notification;
    private readonly AuditTrailService _audit;

    public FileStorageService(LoggingService logger, SearchIndexService search,
        NotificationService notification, AuditTrailService audit)
    {
        _logger = logger;
        _search = search;
        _notification = notification;
        _audit = audit;
    }

    public void Upload(string fileName, byte[] content, string author)
    {
        Console.WriteLine($"[Storage] Uploading '{fileName}'");

        // Manually notify EVERY system — tight coupling
        _logger.Log($"File '{fileName}' uploaded");
        _search.IndexFile(fileName, author);
        _notification.SendUploadAlert(fileName, author);
        _audit.RecordUpload(fileName, author, DateTime.UtcNow);
    }
}
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| Tight coupling | FileStorageService depends on 4 concrete services |
| OCP violation | Adding MetricsCollector = modifying FileStorageService |
| SRP violation | Storage service handles storage + orchestration |
| Rigid | Can't add/remove subscribers at runtime |
| Constructor bloat | More subscribers = more constructor parameters |
| Fragile | Forget to add notification in `Delete()`? Silent bug |

---

## V2 — How to Implement Observer

**Step 1: Define the event data**

```csharp
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
```

**Step 2: Define the Observer interface**

```csharp
public interface IStorageObserver
{
    void OnStorageEvent(StorageEvent storageEvent);
}
```

**Step 3: Define the Subject interface**

```csharp
public interface IStorageSubject
{
    void Subscribe(IStorageObserver observer);
    void Unsubscribe(IStorageObserver observer);
}
```

**Step 4: Implement the Subject (publisher)**

```csharp
public class FileStorageService : IStorageSubject
{
    private readonly List<IStorageObserver> _observers = new();

    public void Subscribe(IStorageObserver observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }

    public void Unsubscribe(IStorageObserver observer) => _observers.Remove(observer);

    private void NotifyAll(StorageEvent storageEvent)
    {
        foreach (var observer in _observers)
            observer.OnStorageEvent(storageEvent);
    }

    public void Upload(string fileName, byte[] content, string author)
    {
        // Core responsibility ONLY
        Console.WriteLine($"[Storage] Uploading '{fileName}'");

        // Notify observers — doesn't know who they are
        NotifyAll(new StorageEvent("Uploaded", fileName, author, content.Length));
    }

    public void Delete(string fileName, string author)
    {
        Console.WriteLine($"[Storage] Deleting '{fileName}'");
        NotifyAll(new StorageEvent("Deleted", fileName, author));
    }
}
```

**Step 5: Implement concrete observers**

```csharp
public class LoggingObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
        => Console.WriteLine($"[Log] {e.EventType}: '{e.FileName}' by {e.Author}");
}

public class SearchIndexObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
    {
        if (e.EventType == "Uploaded")
            Console.WriteLine($"[Search] Indexed '{e.FileName}'");
        else if (e.EventType == "Deleted")
            Console.WriteLine($"[Search] Removed '{e.FileName}'");
    }
}

public class NotificationObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
        => Console.WriteLine($"[Notify] '{e.FileName}' was {e.EventType.ToLower()}");
}

public class AuditTrailObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
        => Console.WriteLine($"[Audit] {e.EventType}: '{e.FileName}' at {e.Timestamp:HH:mm:ss}");
}

// NEW observer — added without modifying FileStorageService!
public class MetricsObserver : IStorageObserver
{
    private int _uploadCount;
    private long _totalBytes;

    public void OnStorageEvent(StorageEvent e)
    {
        if (e.EventType == "Uploaded")
        {
            _uploadCount++;
            _totalBytes += e.FileSizeBytes;
            Console.WriteLine($"[Metrics] Uploads: {_uploadCount}, Total: {_totalBytes} bytes");
        }
    }
}
```

**Step 6: Usage — subscribe at runtime**

```csharp
var storage = new FileStorageService();

// Subscribe observers
storage.Subscribe(new LoggingObserver());
storage.Subscribe(new SearchIndexObserver());
storage.Subscribe(new NotificationObserver());
storage.Subscribe(new AuditTrailObserver());
storage.Subscribe(new MetricsObserver());

// Upload — all 5 observers notified automatically
storage.Upload("report.pdf", content, "Alice");

// Unsubscribe notification, then delete — only 4 observers notified
storage.Unsubscribe(notificationObserver);
storage.Delete("report.pdf", "Alice");
```

---

## When to Use Observer

### Use Observer When:

| Scenario | Why Observer Helps |
|----------|-------------------|
| Multiple systems need to react to the same event | Decouple publisher from subscribers |
| Subscribers change at runtime | Subscribe/unsubscribe dynamically |
| You don't want the publisher to know its subscribers | Publisher only knows the interface |
| Adding new reactions should not modify the publisher | OCP — new observer class, no changes |
| One-to-many relationship (1 event → N reactions) | Single notification dispatches to all |

### Don't Use Observer When:

| Scenario | Why Not |
|----------|---------|
| Only one subscriber exists and won't change | Direct call is simpler |
| Subscribers need to respond in a specific order | Observer doesn't guarantee order |
| You need request/response (subscriber returns a value) | Observer is fire-and-forget |
| Circular dependencies between observers | Can cause infinite notification loops |

### Observer vs Event-Driven vs Pub/Sub:

| Aspect | Observer | C# Events/Delegates | Pub/Sub (Message Broker) |
|--------|----------|--------------------|-----------------------|
| Coupling | Subject knows observer interface | Publisher knows delegate signature | Publisher knows nothing (broker decouples) |
| Communication | In-process, synchronous | In-process, synchronous | Cross-process, async |
| Discovery | Manual subscribe/unsubscribe | += / -= operators | Topic-based, broker manages |
| Scalability | Same process | Same process | Distributed (Kafka, RabbitMQ, SNS) |
| Use case | Domain events within a service | UI events, simple callbacks | Microservice communication |

### Real-World .NET Examples:

| Example | Subject | Observers |
|---------|---------|-----------|
| `INotifyPropertyChanged` | ViewModel | UI bindings (WPF/MAUI) |
| `IObservable<T>` / `IObserver<T>` | Data stream | Reactive subscribers |
| ASP.NET Core `IHostedService` events | Application lifetime | Background services |
| `FileSystemWatcher` | File system | Handlers for Created/Changed/Deleted |
| `IChangeToken` | Configuration source | Config reload handlers |
| MediatR `INotification` | Domain event | Multiple handlers |

---

## Observer vs Mediator

Both patterns deal with communication between objects, but they solve different problems and flow in different directions.

### Core Difference

```
OBSERVER (one-to-many broadcast):
  Subject ──notify──→ Observer A
           ──notify──→ Observer B
           ──notify──→ Observer C
  
  Subject says: "Something happened" → all observers react independently.
  Observers don't know about each other.

MEDIATOR (many-to-many through a hub):
  Component A ──→ Mediator ──→ Component B
  Component B ──→ Mediator ──→ Component A
  Component C ──→ Mediator ──→ Component A, B
  
  Components say: "I want to communicate" → Mediator routes/coordinates.
  Components don't know about each other — Mediator handles all interaction logic.
```

### Comparison Table

| Aspect | Observer | Mediator |
|--------|----------|----------|
| Direction | One-to-many (Subject → Observers) | Many-to-many (Components ↔ Mediator ↔ Components) |
| Who knows whom | Subject knows observer interface; observers don't know each other | Nobody knows anybody — Mediator knows all components |
| Communication | Broadcast — all observers get same event | Routed — Mediator decides who gets what |
| Logic location | Observers contain their own reaction logic | Mediator contains the coordination/routing logic |
| Coupling | Subject → IObserver (loose) | All components → IMediator (centralized) |
| Adding a participant | New observer class (Subject unchanged) | Mediator may need update to route to new component |
| Use case | "Something happened, react however you want" | "These components need to interact in coordinated ways" |

### Code Comparison

**Observer — Subject broadcasts, observers react independently:**

```csharp
// Subject broadcasts — doesn't know or care who's listening
public class FileStorageService : IStorageSubject
{
    private readonly List<IStorageObserver> _observers = new();

    public void Upload(string fileName, byte[] content, string author)
    {
        // Do work
        Console.WriteLine($"[Storage] Uploading '{fileName}'");

        // Broadcast to ALL observers — each reacts on its own
        foreach (var observer in _observers)
            observer.OnStorageEvent(new StorageEvent("Uploaded", fileName, author));
    }
}

// Each observer independently decides how to react
public class LoggingObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
        => Console.WriteLine($"[Log] {e.EventType}: {e.FileName}");
}
```

**Mediator — Components communicate through a central hub:**

```csharp
// Mediator coordinates interaction BETWEEN components
public interface IStorageMediator
{
    void Notify(object sender, string eventType, StorageEvent data);
}

public class StorageMediator : IStorageMediator
{
    private readonly FileStorageService _storage;
    private readonly SearchIndexService _search;
    private readonly NotificationService _notification;
    private readonly QuotaService _quota;

    public StorageMediator(FileStorageService storage, SearchIndexService search,
        NotificationService notification, QuotaService quota)
    {
        _storage = storage;
        _search = search;
        _notification = notification;
        _quota = quota;
    }

    // Mediator contains the COORDINATION LOGIC — who needs to know what
    public void Notify(object sender, string eventType, StorageEvent data)
    {
        if (eventType == "Uploaded")
        {
            _search.IndexFile(data.FileName);
            _notification.SendAlert(data.FileName);

            // Coordination: if quota exceeded after upload, notify storage to reject future uploads
            if (_quota.IsExceeded())
                _storage.PauseUploads();
        }
        else if (eventType == "Deleted")
        {
            _search.RemoveFile(data.FileName);

            // Coordination: if quota was exceeded but now has space, resume
            if (!_quota.IsExceeded())
                _storage.ResumeUploads();
        }
    }
}

// Components only know the mediator — not each other
public class FileStorageService
{
    private readonly IStorageMediator _mediator;

    public FileStorageService(IStorageMediator mediator) => _mediator = mediator;

    public void Upload(string fileName, byte[] content, string author)
    {
        Console.WriteLine($"[Storage] Uploading '{fileName}'");
        _mediator.Notify(this, "Uploaded", new StorageEvent("Uploaded", fileName, author));
    }
}
```

### When to Use Which

| Scenario | Pattern | Why |
|----------|---------|-----|
| "File uploaded — log it, index it, notify" | Observer | Independent reactions, no coordination needed |
| "File uploaded — check quota, if exceeded pause uploads and notify admin" | Mediator | Components need coordinated interaction |
| Broadcasting events (fire-and-forget) | Observer | Observers react independently |
| Complex workflows between components | Mediator | Logic lives in one place (mediator) |
| Adding reactions without modifying publisher | Observer | New observer = new class |
| Components need bidirectional communication | Mediator | Mediator routes messages both ways |
| Reducing N×N dependencies to N×1 | Mediator | All components talk to mediator only |

### Can They Work Together?

Yes. A common architecture uses **Mediator for command routing** and **Observer for event broadcasting**:

```csharp
// MediatR example — combines both concepts:

// Command (Mediator pattern): one handler processes the request
public record UploadFileCommand(string FileName, byte[] Content) : IRequest<bool>;
public class UploadFileHandler : IRequestHandler<UploadFileCommand, bool> { ... }

// Notification (Observer pattern): multiple handlers react to the event
public record FileUploadedEvent(string FileName) : INotification;
public class IndexFileHandler : INotificationHandler<FileUploadedEvent> { ... }
public class SendAlertHandler : INotificationHandler<FileUploadedEvent> { ... }
public class UpdateMetricsHandler : INotificationHandler<FileUploadedEvent> { ... }
```

### Summary

- **Observer:** "Something happened" → broadcast → observers independently react
- **Mediator:** "I need something to happen" → tell mediator → mediator coordinates multiple components
