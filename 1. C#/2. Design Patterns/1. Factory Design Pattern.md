# Factory Design Pattern

## Table of Contents

- [What is the Factory Pattern?](#what-is-the-factory-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Factory?](#v1--why-do-we-need-factory)
- [V2 — How to Implement Factory](#v2--how-to-implement-factory)
- [When to Use Factory](#when-to-use-factory)

---

## What is the Factory Pattern?

The Factory pattern is a **creational design pattern** that provides an interface for creating objects without exposing the instantiation logic to the client. Instead of the client calling `new ConcreteClass()` directly, it delegates creation to a factory method or factory class.

**Core Idea:**
- The client asks the factory for an object by specifying a type/parameter
- The factory decides which concrete class to instantiate
- The client receives the object through an abstraction (interface/base class)
- The client never knows or cares which concrete class was used

**Key Principles Satisfied:**
- **Open/Closed Principle:** Add new types without modifying existing client code
- **Single Responsibility:** Creation logic is separated from business logic
- **Dependency Inversion:** Client depends on abstractions, not concrete classes

---

## UML Diagram

```
┌─────────────────────────────┐
│         «interface»         │
│       IFileRepository       │
├─────────────────────────────┤
│ + Upload(fileName, content) │
│ + Download(fileName)        │
│ + Delete(fileName)          │
└─────────────┬───────────────┘
              │ implements
              │
    ┌─────────┼───────────────┐
    │         │               │
    ▼         ▼               ▼
┌────────┐ ┌──────────┐ ┌───────────┐
│  S3    │ │  Local   │ │ AzureBlob │
│ File   │ │  File    │ │   File    │
│ Repo   │ │  Repo    │ │   Repo    │
├────────┤ ├──────────┤ ├───────────┤
│Upload()│ │ Upload() │ │ Upload()  │
│Down..()│ │ Down..() │ │ Down..()  │
│Delete()│ │ Delete() │ │ Delete()  │
└────────┘ └──────────┘ └───────────┘
    ▲         ▲               ▲
    │         │               │
    └─────────┼───────────────┘
              │ creates
              │
┌─────────────┴───────────────┐
│    FileRepositoryFactory    │
├─────────────────────────────┤
│ + CreateRepository(         │
│     type: StorageType       │
│   ): IFileRepository        │
└─────────────┬───────────────┘
              │
              │ uses
              ▼
┌─────────────────────────────┐
│      DocumentService        │
│         (Client)            │
├─────────────────────────────┤
│ - _repository: IFileRepo    │
├─────────────────────────────┤
│ + UploadDocument()          │
│ + DownloadDocument()        │
│ + DeleteDocument()          │
└─────────────────────────────┘
```

**Relationships:**
- `DocumentService` (Client) → depends on → `FileRepositoryFactory` and `IFileRepository`
- `FileRepositoryFactory` → creates → concrete `IFileRepository` implementations
- Client **never** depends on `S3FileRepository`, `LocalFileRepository`, or `AzureBlobFileRepository` directly

---

## V1 — Why Do We Need Factory?

**The Problem: Client tightly coupled to concrete classes.**

```csharp
string storageType = "s3";

IFileRepository repository;

// This if/switch is duplicated EVERYWHERE a repository is created
if (storageType == "s3")
    repository = new S3FileRepository();
else if (storageType == "local")
    repository = new LocalFileRepository();
else if (storageType == "azure")
    repository = new AzureBlobFileRepository();
else
    throw new ArgumentException($"Unknown storage type: {storageType}");

repository.Upload("report.pdf", content);
```

**What's wrong here:**

| Problem | Explanation |
|---------|-------------|
| Tight coupling | Client knows `S3FileRepository`, `LocalFileRepository`, `AzureBlobFileRepository` by name |
| Violates Open/Closed | Adding `GCSFileRepository` requires changing every file that creates repositories |
| Violates SRP | Client is responsible for both creation logic AND business logic |
| Code duplication | The same switch/if block is repeated across the codebase |
| Hard to test | Can't mock creation — the `new` calls are hardcoded |
| Shotgun surgery | One new provider → changes scattered across many files |

**The classes themselves are fine — it's the CREATION that's the problem.**

```csharp
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
}

public class S3FileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[S3] Uploading '{fileName}' to S3 bucket");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class LocalFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[Local] Writing '{fileName}' to local disk");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class AzureBlobFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[AzureBlob] Uploading '{fileName}' to Blob Storage");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}
```

---

## V2 — How to Implement Factory

**Step 1: Define the Product interface**

```csharp
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
}
```

**Step 2: Create concrete products**

```csharp
public class S3FileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[S3] Uploading '{fileName}' to S3 bucket ({content.Length} bytes)");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class LocalFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[Local] Writing '{fileName}' to local disk ({content.Length} bytes)");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class AzureBlobFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[AzureBlob] Uploading '{fileName}' to Blob Storage ({content.Length} bytes)");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}
```

**Step 3: Create the Factory**

```csharp
public enum StorageType
{
    S3,
    Local,
    AzureBlob
}

public class FileRepositoryFactory
{
    public IFileRepository CreateRepository(StorageType type)
    {
        return type switch
        {
            StorageType.S3 => new S3FileRepository(),
            StorageType.Local => new LocalFileRepository(),
            StorageType.AzureBlob => new AzureBlobFileRepository(),
            _ => throw new ArgumentException($"Unknown storage type: {type}")
        };
    }
}
```

**Step 4: Client uses factory (never touches concrete classes)**

```csharp
public class DocumentService
{
    private readonly IFileRepository _repository;

    public DocumentService(FileRepositoryFactory factory, StorageType storageType)
    {
        _repository = factory.CreateRepository(storageType);
    }

    public void UploadDocument(string fileName, byte[] content)
    {
        _repository.Upload(fileName, content);
    }

    public void DownloadDocument(string fileName)
    {
        _repository.Download(fileName);
    }

    public void DeleteDocument(string fileName)
    {
        _repository.Delete(fileName);
    }
}
```

**Step 5: Usage**

```csharp
var factory = new FileRepositoryFactory();
var service = new DocumentService(factory, StorageType.S3);

service.UploadDocument("report.pdf", new byte[] { 1, 2, 3 });
service.DownloadDocument("report.pdf");
service.DeleteDocument("report.pdf");

// Switching providers — just change the enum
var localService = new DocumentService(factory, StorageType.Local);
localService.UploadDocument("draft.txt", new byte[] { 4, 5, 6 });
```

**Adding a new storage provider (e.g., Google Cloud Storage):**

```csharp
// 1. New class — implements existing interface
public class GCSFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[GCS] Uploading '{fileName}' to Cloud Storage");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

// 2. New enum value
public enum StorageType { S3, Local, AzureBlob, GCS }

// 3. One new case in factory
StorageType.GCS => new GCSFileRepository(),

// 4. Client code (DocumentService) is COMPLETELY UNCHANGED ✓
```

---

## When to Use Factory

| Use Factory When | Don't Use Factory When |
|------------------|------------------------|
| Object creation involves logic/decisions | Only one implementation exists |
| Multiple implementations of same interface | Object creation is trivial (`new Config()`) |
| Client shouldn't know concrete classes | You're adding indirection for no benefit |
| You need to centralize creation for consistency | The pattern adds complexity without solving a problem |
| You want testability (mock the factory) | YAGNI — you don't actually need extension |

**Real-world examples:**
- `DbProviderFactory` in ADO.NET — creates connections without knowing SqlServer vs Postgres
- `LoggerFactory` in Microsoft.Extensions.Logging — creates loggers without knowing Console vs File vs Seq
- `HttpClientFactory` — creates configured HttpClient instances
- Cloud SDKs — storage client factories that abstract the provider
