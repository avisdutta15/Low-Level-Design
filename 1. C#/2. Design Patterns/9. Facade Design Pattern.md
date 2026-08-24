# Facade Design Pattern

## Table of Contents

- [What is the Facade Pattern?](#what-is-the-facade-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Facade?](#v1--why-do-we-need-facade)
- [V2 — How to Implement Facade](#v2--how-to-implement-facade)
- [Facade vs Adapter vs Decorator](#facade-vs-adapter-vs-decorator)
- [When to Use Facade](#when-to-use-facade)

---

## What is the Facade Pattern?

The Facade pattern is a **structural design pattern** that provides a simplified, unified interface to a complex subsystem of classes. It doesn't add new functionality — it just makes existing functionality easier to use by hiding orchestration complexity behind a single entry point.

**Core Idea:**
- A complex operation requires coordinating multiple services in a specific order
- The Facade wraps that coordination into a single, simple method
- Clients call the Facade instead of wiring services together themselves
- The subsystem services remain accessible directly for advanced use cases

**Key Distinction:**
- Facade doesn't change interfaces (Adapter)
- Facade doesn't add behavior (Decorator)
- Facade **simplifies access** to a complex subsystem by providing a higher-level API

---

## UML Diagram

```
┌─────────────────────┐
│      Client         │
│  (API Controller)   │
└─────────┬───────────┘
          │ calls ONE method
          ▼
┌─────────────────────────────────────────────────┐
│          DocumentStorageFacade                    │
│               (THE FACADE)                       │
├─────────────────────────────────────────────────┤
│ + UploadDocument(fileName, content, author)      │
│ + DeleteDocument(fileName)                       │
│ + DownloadDocument(fileName)                     │
│ + SearchDocuments(query)                         │
├─────────────────────────────────────────────────┤
│ - _fileStorage: FileStorageService               │
│ - _metadata: MetadataService                     │
│ - _search: SearchIndexService                    │
│ - _virusScan: VirusScanService                   │
│ - _notification: NotificationService             │
└─────────┬───────────────────────────────────────┘
          │ orchestrates (correct order + error handling)
          │
    ┌─────┼─────────┬──────────────┬──────────────┐
    │     │         │              │              │
    ▼     ▼         ▼              ▼              ▼
┌──────┐┌──────┐┌───────────┐┌──────────┐┌────────────┐
│Virus ││File  ││ Metadata  ││  Search  ││Notification│
│Scan  ││Store ││ Service   ││  Index   ││  Service   │
│Svc   ││Svc   ││           ││  Service ││            │
├──────┤├──────┤├───────────┤├──────────┤├────────────┤
│Scan()││Upload││SaveMeta() ││Index()   ││NotifyUp()  │
│      ││Down()││GetMeta()  ││Search()  ││NotifyDel() │
│      ││Del() ││DeleteMeta ││Remove()  ││            │
└──────┘└──────┘└───────────┘└──────────┘└────────────┘
   ①       ②         ③            ④           ⑤

Upload flow (orchestrated by Facade):
  ① VirusScan.Scan(content)        → reject if infected
  ② FileStorage.Upload(file)       → store the bytes
  ③ Metadata.Save(id, author, ...) → record metadata
  ④ Search.Index(id, content, ...) → make searchable
  ⑤ Notification.NotifyUpload()    → alert subscribers
```

---

## V1 — Why Do We Need Facade?

**Scenario:** Uploading a document requires orchestrating 5 services in the correct order.

**Without Facade — client does all the orchestration:**

```csharp
var fileStorage = new FileStorageService();
var metadata = new MetadataService();
var search = new SearchIndexService();
var virusScan = new VirusScanService();
var notification = new NotificationService();

// Client must coordinate 5 services in correct order:
bool isSafe = virusScan.Scan(content);
if (!isSafe) return;

fileStorage.Upload(fileName, content);
metadata.SaveMetadata(fileName, author, content.Length, "application/pdf");

var metadataDict = new Dictionary<string, string> { ["author"] = author };
search.IndexDocument(fileName, "quarterly report", metadataDict);

notification.NotifyUpload(fileName, author);
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| Client complexity | Every client must know 5 services + call order |
| Tight coupling | Client depends on FileStorage, Metadata, Search, VirusScan, Notification |
| Duplicated orchestration | Every controller/handler repeats the same 5-step sequence |
| Fragile | Change the upload process = modify every client |
| Hard to test | Must mock 5 services to test any client |
| Order dependency | Virus scan BEFORE upload, metadata BEFORE index — easy to get wrong |

---

## V2 — How to Implement Facade

**Step 1: Keep the subsystem services as-is (don't modify them)**

```csharp
public class FileStorageService
{
    public void Upload(string fileName, byte[] content) { ... }
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class MetadataService { ... }
public class SearchIndexService { ... }
public class VirusScanService { ... }
public class NotificationService { ... }
```

**Step 2: Create the Facade**

```csharp
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

    public bool UploadDocument(string fileName, byte[] content, string author, string contentType)
    {
        // Facade orchestrates all 5 services in correct order:
        if (!_virusScan.Scan(content))
            return false;

        _fileStorage.Upload(fileName, content);
        _metadata.SaveMetadata(fileName, author, content.Length, contentType);

        var meta = new Dictionary<string, string> { ["author"] = author, ["contentType"] = contentType };
        _search.IndexDocument(fileName, fileName, meta);

        _notification.NotifyUpload(fileName, author);
        return true;
    }

    public void DeleteDocument(string fileName)
    {
        _fileStorage.Delete(fileName);
        _metadata.DeleteMetadata(fileName);
        _search.RemoveFromIndex(fileName);
        _notification.NotifyDeletion(fileName);
    }

    public byte[] DownloadDocument(string fileName) => _fileStorage.Download(fileName);
    public List<string> SearchDocuments(string query) => _search.Search(query);
}
```

**Step 3: Client uses the Facade**

```csharp
var facade = new DocumentStorageFacade(
    new FileStorageService(),
    new MetadataService(),
    new SearchIndexService(),
    new VirusScanService(),
    new NotificationService()
);

// ONE call — Facade handles all 5 services
facade.UploadDocument("report.pdf", content, "Alice", "application/pdf");
facade.DeleteDocument("report.pdf");
var results = facade.SearchDocuments("quarterly report");
```

**Before and After:**

```
BEFORE (V1): Client coordinates 5 services directly
  client → VirusScanService.Scan()
  client → FileStorageService.Upload()
  client → MetadataService.Save()
  client → SearchIndexService.Index()
  client → NotificationService.Notify()
  (5 dependencies, correct order required, duplicated everywhere)

AFTER (V2): Client calls Facade
  client → DocumentStorageFacade.Upload()
  (1 dependency, orchestration hidden, defined once)
```

---

## Facade vs Adapter vs Decorator

| Aspect | Facade | Adapter | Decorator |
|--------|--------|---------|-----------|
| Purpose | **Simplify** access to a complex subsystem | **Convert** an incompatible interface | **Add behavior** to an existing object |
| Number of wrapped objects | **Multiple** services (5 in our example) | **One** incompatible object | **One** object with same interface |
| Interface change | Creates a **new, simpler** interface | Makes existing interface **look like** another | **Same** interface as the wrapped object |
| Behavior change | No — same functionality, simpler API | No — same functionality, different interface | Yes — adds logging, caching, retry, etc. |
| Stacking | Not applicable | Not applicable | Multiple decorators stack on each other |
| Client knows subsystem? | No — only sees the Facade | No — only sees the Target interface | No — only sees the component interface |

**Visual comparison with our storage theme:**

```
FACADE (simplifies complex subsystem):
  Client → DocumentStorageFacade.Upload()
              ├── VirusScanService.Scan()
              ├── FileStorageService.Upload()
              ├── MetadataService.Save()
              ├── SearchIndexService.Index()
              └── NotificationService.Notify()
  "One method wraps MANY services"

ADAPTER (converts incompatible interface):
  Client → AzureBlobAdapter.Upload(fileName, byte[])
              └── ThirdPartyAzureClient.PutBlob(container, blob, Stream)
  "Make X look like Y"

DECORATOR (adds behavior, same interface):
  Client → LoggingDecorator.Upload()
              └── CachingDecorator.Upload()
                    └── S3FileRepository.Upload()
  "Same interface, extra behavior at each layer"
```

**Key distinctions:**
- Facade: "I don't want to deal with 5 services — give me ONE simple API"
- Adapter: "I have an incompatible class — make it fit my interface"
- Decorator: "I have a compatible class — add logging/caching/retry to it"

**Can they work together?**

```csharp
// Facade orchestrates the high-level workflow
var facade = new DocumentStorageFacade(
    fileStorage,    // ← this could be a Decorator (LoggingDecorator wrapping S3)
    metadata,       // ← this could use an Adapter (wrapping a third-party DB client)
    search,
    virusScan,
    notification
);
```

Facade operates at a higher level — it coordinates services. Those services internally might use Adapters or Decorators.

---

## When to Use Facade

### Use Facade When:

| Scenario | Why Facade Helps |
|----------|------------------|
| A workflow requires coordinating multiple services | One method call instead of N |
| Clients don't need to know subsystem details | Facade hides complexity |
| You want to decouple clients from subsystem internals | Change internals without affecting clients |
| Multiple clients repeat the same orchestration | Define it once in the Facade |
| You're building a library/SDK for others to consume | Simple API, powerful internals |
| You need a single point for error handling/rollback | Facade coordinates recovery |

### Don't Use Facade When:

| Scenario | Why Not |
|----------|---------|
| Client needs fine-grained control over each step | Facade hides too much |
| Only one service is involved | No orchestration needed |
| The subsystem is already simple | Facade adds unnecessary indirection |
| You're using it to hide bad design | Fix the design instead |

### Real-World .NET Examples:

| Facade | What It Simplifies |
|--------|-------------------|
| `WebApplication.CreateBuilder()` | Orchestrates DI, config, logging, Kestrel, middleware setup |
| `HttpClient` | Facades DNS resolution, TCP connection, TLS handshake, HTTP framing |
| `SmtpClient.Send()` | Facades DNS lookup, socket connect, EHLO, AUTH, DATA, QUIT |
| `DbContext.SaveChanges()` | Facades change tracking, SQL generation, connection open, transaction, commit |
| `File.WriteAllText()` | Facades FileStream creation, encoding, StreamWriter, flush, close |

### Facade vs "God Class":

A Facade is NOT a God class. The difference:
- **Facade**: delegates to subsystem services, contains no business logic itself
- **God class**: implements everything directly, becoming unmaintainable

The Facade should contain **orchestration logic only** — the order of calls, error handling, and coordination. The actual work lives in the subsystem services.
