# Adapter Design Pattern

## Table of Contents

- [What is the Adapter Pattern?](#what-is-the-adapter-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Adapter?](#v1--why-do-we-need-adapter)
- [V2 — How to Implement Adapter](#v2--how-to-implement-adapter)
- [When to Use Adapter](#when-to-use-adapter)

---

## What is the Adapter Pattern?

The Adapter pattern is a **structural design pattern** that allows objects with incompatible interfaces to work together. It wraps an existing class with a new interface so that it becomes compatible with what the client expects.

**Core Idea:**
- You have a **Target** interface that your application depends on
- You have an **Adaptee** (third-party class, legacy code) with an incompatible interface
- You can't modify either one
- The **Adapter** implements the Target interface and internally delegates to the Adaptee, translating calls between the two

**Real-world analogy:** A power adapter converts a US plug (your laptop) to work with a European socket (the wall). It doesn't modify the laptop or the socket — it translates between them.

---

## UML Diagram

```
┌─────────────────────────────────────────┐
│       «interface» IFileRepository       │
│              (Target)                    │
├─────────────────────────────────────────┤
│ + Upload(fileName, byte[] content)      │
│ + Download(fileName): byte[]            │
│ + Delete(fileName)                      │
│ + Exists(fileName): bool                │
└──────────────┬──────────────────────────┘
               │ implements
               │
     ┌─────────┴──────────┐
     │                    │
     ▼                    ▼
┌──────────────┐   ┌──────────────────────────────────┐
│S3FileRepo    │   │     AzureBlobAdapter              │
│(native impl) │   │        (THE ADAPTER)              │
├──────────────┤   ├──────────────────────────────────┤
│+Upload()     │   │ - _azureClient: ThirdPartyAzure  │
│+Download()   │   │ - _containerName: string          │
│+Delete()     │   ├──────────────────────────────────┤
│+Exists()     │   │ + Upload() → calls PutBlob()     │
└──────────────┘   │ + Download() → calls GetBlob()   │
                   │ + Delete() → calls RemoveBlob()  │
                   │ + Exists() → calls BlobExists()  │
                   └──────────────┬───────────────────┘
                                  │ delegates to
                                  ▼
                   ┌──────────────────────────────────┐
                   │   ThirdPartyAzureBlobClient      │
                   │          (Adaptee)               │
                   ├──────────────────────────────────┤
                   │ + PutBlob(container, blob,       │
                   │           Stream, contentType)   │
                   │ + GetBlob(container, blob)       │
                   │           : Stream               │
                   │ + RemoveBlob(container, blob)    │
                   │ + BlobExists(container, blob)    │
                   │           : bool                 │
                   └──────────────────────────────────┘

                            ▲
                            │ depends on IFileRepository only
                            │
                   ┌────────┴─────────┐
                   │  DocumentService │
                   │    (Client)      │
                   ├──────────────────┤
                   │- _repository     │
                   ├──────────────────┤
                   │+ UploadDocument()│
                   │+ DownloadDoc()   │
                   │+ DeleteDocument()│
                   └──────────────────┘
```

**Key Relationships:**
- `DocumentService` depends ONLY on `IFileRepository` — never changes
- `S3FileRepository` implements `IFileRepository` natively — no adapter needed
- `AzureBlobAdapter` implements `IFileRepository` and wraps `ThirdPartyAzureBlobClient`
- `ThirdPartyAzureBlobClient` is unchanged — we don't own it

---

## V1 — Why Do We Need Adapter?

**Scenario:** Our application uses `IFileRepository` everywhere. We have a working `S3FileRepository`. Now we need to integrate a third-party Azure Blob SDK with a completely different interface.

**Our interface:**

```csharp
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
    bool Exists(string fileName);
}
```

**The third-party SDK we don't own:**

```csharp
public class ThirdPartyAzureBlobClient
{
    public void PutBlob(string containerName, string blobPath, Stream content, string contentType) { ... }
    public Stream GetBlob(string containerName, string blobPath) { ... }
    public void RemoveBlob(string containerName, string blobPath) { ... }
    public bool BlobExists(string containerName, string blobPath) { ... }
}
```

**The incompatibilities:**

| Our Interface | Azure SDK | Mismatch |
|---------------|-----------|----------|
| `Upload(fileName, byte[])` | `PutBlob(container, blob, Stream, contentType)` | Different name, types, extra params |
| `Download(fileName): byte[]` | `GetBlob(container, blob): Stream` | Different name, return type, extra param |
| `Delete(fileName)` | `RemoveBlob(container, blob)` | Different name, extra param |
| `Exists(fileName): bool` | `BlobExists(container, blob): bool` | Different name, extra param |

**We can't modify either side:**
- `IFileRepository` — hundreds of services depend on it
- `ThirdPartyAzureBlobClient` — it's from a NuGet package

**Without Adapter, our options are all bad:**
1. Modify `IFileRepository` → breaks all existing services
2. Modify the third-party SDK → we don't own it
3. Rewrite `DocumentService` for Azure → code duplication, violates DIP

---

## V2 — How to Implement Adapter

**Step 1: Keep the Target interface unchanged**

```csharp
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
    bool Exists(string fileName);
}
```

**Step 2: Keep the Adaptee unchanged (we don't own it)**

```csharp
public class ThirdPartyAzureBlobClient
{
    public void PutBlob(string containerName, string blobPath, Stream content, string contentType) { ... }
    public Stream GetBlob(string containerName, string blobPath) { ... }
    public void RemoveBlob(string containerName, string blobPath) { ... }
    public bool BlobExists(string containerName, string blobPath) { ... }
}
```

**Step 3: Create the Adapter**

```csharp
public class AzureBlobAdapter : IFileRepository
{
    private readonly ThirdPartyAzureBlobClient _azureClient;
    private readonly string _containerName;

    public AzureBlobAdapter(ThirdPartyAzureBlobClient azureClient, string containerName)
    {
        _azureClient = azureClient;
        _containerName = containerName;
    }

    public void Upload(string fileName, byte[] content)
    {
        // Translate: byte[] -> Stream, inject containerName
        using var stream = new MemoryStream(content);
        _azureClient.PutBlob(_containerName, fileName, stream);
    }

    public byte[] Download(string fileName)
    {
        // Translate: Stream -> byte[], inject containerName
        using var stream = _azureClient.GetBlob(_containerName, fileName);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public void Delete(string fileName)
    {
        // Translate: Delete -> RemoveBlob, inject containerName
        _azureClient.RemoveBlob(_containerName, fileName);
    }

    public bool Exists(string fileName)
    {
        // Translate: Exists -> BlobExists, inject containerName
        return _azureClient.BlobExists(_containerName, fileName);
    }
}
```

**Step 4: Client uses the adapter transparently**

```csharp
// The third-party client
var azureClient = new ThirdPartyAzureBlobClient();

// Wrap it with our adapter
IFileRepository azureRepo = new AzureBlobAdapter(azureClient, "my-container");

// DocumentService doesn't know it's talking to Azure!
var service = new DocumentService(azureRepo);
service.UploadDocument("invoice.pdf", new byte[] { 4, 5, 6 });
service.DownloadDocument("invoice.pdf");
service.DeleteDocument("invoice.pdf");
```

**The translation map:**

```
Our Interface                    Adapter translates to Azure SDK
─────────────                    ──────────────────────────────
Upload(fileName, byte[])    →    PutBlob(container, fileName, new MemoryStream(bytes))
Download(fileName): byte[]  →    GetBlob(container, fileName).CopyTo(ms); ms.ToArray()
Delete(fileName)            →    RemoveBlob(container, fileName)
Exists(fileName): bool      →    BlobExists(container, fileName)
```

---

## When to Use Adapter

### Use Adapter When:

| Scenario | Why Adapter Helps |
|----------|-------------------|
| Integrating a third-party library with incompatible API | Wraps it behind your interface |
| Working with legacy code that can't be modified | Adapter translates without touching the original |
| Multiple providers with different APIs for same concept | Each gets its own adapter behind one interface |
| Migrating between systems incrementally | Adapter lets old and new coexist |
| Testing against external services | Adapter can be mocked at the interface level |

### Don't Use Adapter When:

| Scenario | Why Not |
|----------|---------|
| You own both interfaces and can change them | Just make them compatible directly |
| The interfaces are identical | No translation needed |
| The adaptation logic is extremely complex | Consider Facade instead (simplifies, not just translates) |
| You're creating adapters for everything | Might indicate a design problem upstream |

### Adapter vs Facade vs Decorator:

| Pattern | Purpose | Example |
|---------|---------|---------|
| Adapter | Make incompatible interface compatible | Azure SDK → IFileRepository |
| Facade | Simplify a complex subsystem into one interface | Multiple AWS services → one `StorageManager` |
| Decorator | Add behavior without changing interface | `LoggingFileRepository` wraps `S3FileRepository`, adds logging |

### Real-World .NET Examples:

| Adapter | What It Adapts |
|---------|---------------|
| `StreamReader` / `StreamWriter` | Adapts `Stream` (bytes) to text-based reading/writing |
| `DataAdapter` (ADO.NET) | Adapts between `DataSet` and database commands |
| `ILogger` wrappers | Adapts Serilog/NLog/log4net behind Microsoft's `ILogger` |
| `HttpClientHandler` | Adapts platform-specific HTTP to `HttpClient` interface |
| Custom SDK wrappers | Adapts cloud provider SDKs behind your domain interfaces |
