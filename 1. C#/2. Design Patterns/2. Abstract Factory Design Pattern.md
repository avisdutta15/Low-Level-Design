# Abstract Factory Design Pattern

## Table of Contents

- [What is the Abstract Factory Pattern?](#what-is-the-abstract-factory-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Abstract Factory?](#v1--why-do-we-need-abstract-factory)
- [V2 — How to Implement Abstract Factory](#v2--how-to-implement-abstract-factory)
- [Factory vs Abstract Factory](#factory-vs-abstract-factory)
- [When to Use What](#when-to-use-what)

---

## What is the Abstract Factory Pattern?

The Abstract Factory is a **creational design pattern** that provides an interface for creating **families of related objects** without specifying their concrete classes. It's a "factory of factories" — each concrete factory creates a complete, consistent set of products.

**Core Idea:**
- You have multiple product types (FileRepository, MetadataRepository, SearchIndex) that come in variants (AWS, Local, Azure)
- Each variant forms a **family** — all repositories must be from the same infrastructure
- A single abstract factory interface defines methods to create all products in the family
- Concrete factories implement the interface for each variant

**Key difference from simple Factory:**
- Simple Factory: creates ONE product type with different implementations
- Abstract Factory: creates MULTIPLE related product types that must stay consistent

---

## UML Diagram

```
┌───────────────────────────────────────────────────────────────────────┐
│                    «interface» IStorageFactory                        │
├───────────────────────────────────────────────────────────────────────┤
│ + CreateFileRepository(): IFileRepository                             │
│ + CreateMetadataRepository(): IMetadataRepository                     │
│ + CreateSearchIndex(): ISearchIndex                                   │
└───────────────────────┬───────────────────────────┬───────────────────┘
                        │                           │
           implements   │                           │   implements
                        │                           │
         ┌──────────────▼──────────────┐  ┌────────▼──────────────────────┐
         │      AwsStorageFactory      │  │     LocalStorageFactory       │
         ├─────────────────────────────┤  ├───────────────────────────────┤
         │ + CreateFileRepository()    │  │ + CreateFileRepository()      │
         │ + CreateMetadataRepository()│  │ + CreateMetadataRepository()  │
         │ + CreateSearchIndex()       │  │ + CreateSearchIndex()         │
         └──────────┬──────────────────┘  └──────────┬────────────────────┘
                    │ creates                        │ creates
                    │                                │
                    ▼                                ▼
┌────────────────────────────────┐  ┌────────────────────────────────┐
│  S3FileRepository              │  │  LocalFileRepository           │
│  DynamoDbMetadataRepository    │  │  SqliteMetadataRepository      │
│  ElasticSearchIndex            │  │  InMemorySearchIndex           │
└────────────────────────────────┘  └────────────────────────────────┘
                    │                                 │
                    │ implements                      │ implements
                    ▼                                 ▼
┌───────────────────────────────────────────────────────────────────────┐
│              «interface» IFileRepository                              │
│              «interface» IMetadataRepository                          │
│              «interface» ISearchIndex                                 │
└───────────────────────────────────────────────────────────────────────┘
                                 ▲
                                 │ depends on (abstractions only)
                                 │
                    ┌────────────┴────────────┐
                    │     DocumentService     │
                    │        (Client)         │
                    ├─────────────────────────┤
                    │ - _fileRepo             │
                    │ - _metadataRepo         │
                    │ - _searchIndex          │
                    ├─────────────────────────┤
                    │ + UploadDocument()      │
                    │ + SearchDocuments()     │
                    │ + DeleteDocument()      │
                    └─────────────────────────┘
```

**Key Relationships:**
- `DocumentService` depends on `IStorageFactory` + abstract repository interfaces only
- `AwsStorageFactory` creates S3 + DynamoDB + ElasticSearch — guaranteed AWS consistency
- `LocalStorageFactory` creates Local disk + SQLite + InMemory — guaranteed local consistency
- Impossible to get S3 files + SQLite metadata (different infrastructure assumptions)

---

## V1 — Why Do We Need Abstract Factory?

**Scenario:** A document management service with multiple storage backends.
- **Production (AWS):** files → S3, metadata → DynamoDB, search → ElasticSearch
- **Development (local):** files → disk, metadata → SQLite, search → in-memory

These form **families** — you can't mix S3 files with SQLite metadata in prod because they assume different infrastructure (VPC, IAM, credentials vs local file paths).

**The problem without Abstract Factory:**

```csharp
string environment = "production";

IFileRepository fileRepo;
IMetadataRepository metadataRepo;
ISearchIndex searchIndex;

// Client must manually ensure ALL repositories match the same environment
if (environment == "production")
{
    fileRepo = new S3FileRepository();
    metadataRepo = new DynamoDbMetadataRepository();
    searchIndex = new ElasticSearchIndex();
}
else if (environment == "development")
{
    fileRepo = new LocalFileRepository();
    metadataRepo = new SqliteMetadataRepository();
    searchIndex = new InMemorySearchIndex();
}
else
{
    throw new ArgumentException($"Unknown environment: {environment}");
}
```

**What's wrong:**

| Problem | Explanation |
|---------|-------------|
| No consistency guarantee | `new S3FileRepository()` + `new SqliteMetadataRepository()` compiles fine — but breaks at runtime |
| Repeated if/else per repo | Every service that needs storage must repeat the environment check |
| Adding a new environment | Staging/testing = modifying every if/else block in the codebase |
| Adding a new repository | CacheRepository = updating every if/else block to add one more line |
| Infrastructure mismatch | S3 expects AWS creds, SQLite expects local path — mixing them silently corrupts data |

**The dangerous mix that compiles fine:**

```csharp
// This compiles but is WRONG
var fileRepo = new S3FileRepository();          // expects AWS credentials + bucket
var metadataRepo = new SqliteMetadataRepository(); // expects local disk path
// File stored in cloud, metadata stored locally → completely disconnected!
```

---

## V2 — How to Implement Abstract Factory

**Step 1: Define abstract product interfaces**

```csharp
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
}

public interface IMetadataRepository
{
    void Save(string key, Dictionary<string, string> metadata);
    Dictionary<string, string>? Get(string key);
    void Delete(string key);
}

public interface ISearchIndex
{
    void Index(string documentId, string content);
    List<string> Search(string query);
}
```

**Step 2: Define the Abstract Factory interface**

```csharp
public interface IStorageFactory
{
    IFileRepository CreateFileRepository();
    IMetadataRepository CreateMetadataRepository();
    ISearchIndex CreateSearchIndex();
}
```

**Step 3: Create concrete products for each family**

```csharp
// AWS family
public class S3FileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[S3] Uploading '{fileName}' to S3 bucket");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class DynamoDbMetadataRepository : IMetadataRepository
{
    public void Save(string key, Dictionary<string, string> metadata)
        => Console.WriteLine($"[DynamoDB] Saving metadata for key '{key}'");
    public Dictionary<string, string>? Get(string key) { ... }
    public void Delete(string key) { ... }
}

public class ElasticSearchIndex : ISearchIndex
{
    public void Index(string documentId, string content)
        => Console.WriteLine($"[ElasticSearch] Indexing document '{documentId}'");
    public List<string> Search(string query) { ... }
}

// Local family
public class LocalFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[Local] Writing '{fileName}' to disk");
    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class SqliteMetadataRepository : IMetadataRepository
{
    public void Save(string key, Dictionary<string, string> metadata)
        => Console.WriteLine($"[SQLite] Saving metadata for key '{key}'");
    public Dictionary<string, string>? Get(string key) { ... }
    public void Delete(string key) { ... }
}

public class InMemorySearchIndex : ISearchIndex
{
    public void Index(string documentId, string content)
        => Console.WriteLine($"[InMemory] Indexing document '{documentId}'");
    public List<string> Search(string query) { ... }
}
```

**Step 4: Create concrete factories**

```csharp
public class AwsStorageFactory : IStorageFactory
{
    public IFileRepository CreateFileRepository() => new S3FileRepository();
    public IMetadataRepository CreateMetadataRepository() => new DynamoDbMetadataRepository();
    public ISearchIndex CreateSearchIndex() => new ElasticSearchIndex();
}

public class LocalStorageFactory : IStorageFactory
{
    public IFileRepository CreateFileRepository() => new LocalFileRepository();
    public IMetadataRepository CreateMetadataRepository() => new SqliteMetadataRepository();
    public ISearchIndex CreateSearchIndex() => new InMemorySearchIndex();
}
```

**Step 5: Client uses abstract factory**

```csharp
public class DocumentService
{
    private readonly IFileRepository _fileRepo;
    private readonly IMetadataRepository _metadataRepo;
    private readonly ISearchIndex _searchIndex;

    public DocumentService(IStorageFactory factory)
    {
        // All repositories come from the SAME factory → guaranteed consistency
        _fileRepo = factory.CreateFileRepository();
        _metadataRepo = factory.CreateMetadataRepository();
        _searchIndex = factory.CreateSearchIndex();
    }

    public void UploadDocument(string fileName, byte[] content, string author)
    {
        _fileRepo.Upload(fileName, content);
        _metadataRepo.Save(fileName, new Dictionary<string, string>
        {
            ["author"] = author,
            ["uploadedAt"] = DateTime.UtcNow.ToString("O")
        });
        _searchIndex.Index(fileName, $"Document by {author}");
    }

    public void SearchDocuments(string query)
    {
        var results = _searchIndex.Search(query);
        Console.WriteLine($"Found {results.Count} result(s)");
    }

    public void DeleteDocument(string fileName)
    {
        _fileRepo.Delete(fileName);
        _metadataRepo.Delete(fileName);
    }
}
```

**Step 6: Usage — factory selection happens in ONE place**

```csharp
string environment = "production"; // from config/env variable

IStorageFactory factory = environment switch
{
    "production" => new AwsStorageFactory(),
    "development" => new LocalStorageFactory(),
    _ => throw new ArgumentException($"Unknown environment: {environment}")
};

var docService = new DocumentService(factory);
docService.UploadDocument("report.pdf", content, "Alice");
docService.SearchDocuments("quarterly report");
```

**Adding a new environment (Azure):**

```csharp
// 1. New concrete products
public class AzureBlobFileRepository : IFileRepository { ... }
public class CosmosDbMetadataRepository : IMetadataRepository { ... }
public class AzureCognitiveSearchIndex : ISearchIndex { ... }

// 2. New concrete factory
public class AzureStorageFactory : IStorageFactory
{
    public IFileRepository CreateFileRepository() => new AzureBlobFileRepository();
    public IMetadataRepository CreateMetadataRepository() => new CosmosDbMetadataRepository();
    public ISearchIndex CreateSearchIndex() => new AzureCognitiveSearchIndex();
}

// 3. One new case in the environment switch
"azure" => new AzureStorageFactory(),

// 4. DocumentService is COMPLETELY UNCHANGED ✓
```

---

## Factory vs Abstract Factory

| Aspect | Factory | Abstract Factory |
|--------|---------|------------------|
| Creates | ONE product type | FAMILY of related product types |
| Interface | One `Create` method returning one type | Multiple `Create` methods returning different types |
| Purpose | Decouple client from a single concrete class | Ensure consistency across related products |
| Complexity | Lower — one interface, one factory | Higher — multiple interfaces, multiple factories |
| Example | `NotificationFactory.Create(SMS)` → `INotification` | `IStorageFactory.CreateFileRepo()`, `.CreateMetadataRepo()`, `.CreateSearchIndex()` |
| Consistency | Not applicable — single product | Guaranteed — all products from same family |
| Adding a variant | One new class + one factory case | One new class + one case in ONE factory |
| Adding a product type | N/A (factory creates one type) | New method in IFactory + implementation in ALL concrete factories |

**Visual difference:**

```
Factory (one dimension — different implementations of ONE interface):
  NotificationFactory.Create(Email) → EmailNotification
  NotificationFactory.Create(SMS)   → SmsNotification
  NotificationFactory.Create(Push)  → PushNotification

Abstract Factory (two dimensions — multiple types × multiple families):
  AwsStorageFactory.CreateFileRepository()     → S3FileRepository
  AwsStorageFactory.CreateMetadataRepository() → DynamoDbMetadataRepository
  AwsStorageFactory.CreateSearchIndex()        → ElasticSearchIndex

  LocalStorageFactory.CreateFileRepository()     → LocalFileRepository
  LocalStorageFactory.CreateMetadataRepository() → SqliteMetadataRepository
  LocalStorageFactory.CreateSearchIndex()        → InMemorySearchIndex
```

**Relationship:** Abstract Factory is often implemented using Factory Methods internally. Each `Create` method in the abstract factory IS a factory method.

---

## When to Use What

### Use Simple Factory When:

- You have **one product interface** with multiple implementations
- Client needs to choose which implementation at runtime
- Products are **independent** — no family relationship
- Adding a new variant is the common extension point

**Examples:**
- `NotificationFactory` → Email, SMS, Push (independent — can use SMS without Email)
- `LoggerFactory` → ConsoleLogger, FileLogger, SeqLogger
- `PaymentProcessorFactory` → Stripe, PayPal, Square
- `SerializerFactory` → JsonSerializer, XmlSerializer, YamlSerializer

### Use Abstract Factory When:

- You have **multiple product types** that form **families**
- Products within a family **must be used together** consistently
- Mixing products across families would be a **bug**
- Adding a new family is the common extension point

**Examples:**
- Storage backends (FileRepo + MetadataRepo + SearchIndex per environment)
- Cross-platform UI toolkits (Button + TextBox + Checkbox per platform)
- Database access layers (Connection + Command + DataReader per provider)
- Cloud SDKs (Storage + Queue + Cache per cloud provider)

### Decision Flowchart:

```
Do you have multiple product TYPES that must be consistent?
│
├── NO → Do you need to decouple creation from usage?
│         ├── YES → Use Simple Factory
│         └── NO  → Just use `new` (no pattern needed)
│
└── YES → Can products from different families be mixed safely?
          ├── YES → Use separate Simple Factories (one per product type)
          └── NO  → Use Abstract Factory
```

### Common Mistake:

Using Abstract Factory when you only have one product type. If you only create file repositories (no metadata, no search), a simple `FileRepositoryFactory` is sufficient. Abstract Factory adds indirection that isn't justified for a single product dimension.

---

## Real-World .NET Examples

| Pattern | .NET Example |
|---------|-------------|
| Simple Factory | `LoggerFactory.CreateLogger<T>()` — one product type |
| Abstract Factory | `DbProviderFactory` — creates `DbConnection` + `DbCommand` + `DbDataAdapter` (family per provider: SqlServer, Postgres, SQLite) |
| Abstract Factory | Storage SDK factories — creates BlobClient + TableClient + QueueClient (consistent per cloud provider) |
