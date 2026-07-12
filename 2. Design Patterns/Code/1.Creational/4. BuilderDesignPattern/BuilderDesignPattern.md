# Builder Design Pattern

## Table of Contents

- [What is the Builder Pattern?](#what-is-the-builder-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Builder?](#v1--why-do-we-need-builder)
- [V2 — How to Implement Builder (Fluent)](#v2--how-to-implement-builder-fluent)
- [When to Use Builder](#when-to-use-builder)

---

## What is the Builder Pattern?

The Builder pattern is a **creational design pattern** that separates the construction of a complex object from its representation. It lets you construct objects step-by-step, selecting only the parts you need, while enforcing validation rules at build time.

**Core Idea:**
- Complex objects have many optional parameters with sensible defaults
- A Builder class accumulates configuration step-by-step
- Each step returns `this` (fluent API) for method chaining
- A final `Build()` method validates and creates the immutable product
- The product itself has a private/internal constructor — can only be created via builder

**Key Problems It Solves:**
- Telescoping constructors (too many parameters)
- Positional ambiguity (`new Config("s3", true, false, 30, null, true)` — what does each mean?)
- Invalid object state (encryption enabled without a key)
- Forced specification of optional values (null parades)

---

## UML Diagram

```
┌─────────────────────────────────────────────────┐
│            StorageConfigBuilder                  │
├─────────────────────────────────────────────────┤
│ - _provider: string        [required]           │
│ - _bucketName: string      [required]           │
│ - _region: string          = "us-east-1"        │
│ - _maxRetries: int         = 3                  │
│ - _timeoutSeconds: int     = 30                 │
│ - _enableEncryption: bool  = false              │
│ - _encryptionKey: string?  = null               │
│ - _enableVersioning: bool  = false              │
│ - _enableLogging: bool     = false              │
│ - _logPath: string?        = null               │
│ - _maxFileSizeBytes: long  = MaxValue           │
│ - _allowedExtensions: string[] = []             │
├─────────────────────────────────────────────────┤
│ + StorageConfigBuilder(provider, bucketName)     │
│ + WithRegion(region): StorageConfigBuilder       │
│ + WithMaxRetries(n): StorageConfigBuilder        │
│ + WithTimeout(s): StorageConfigBuilder           │
│ + WithEncryption(key): StorageConfigBuilder      │
│ + WithVersioning(): StorageConfigBuilder         │
│ + WithLogging(path): StorageConfigBuilder        │
│ + WithMaxFileSize(bytes): StorageConfigBuilder   │
│ + WithAllowedExtensions(...): StorageConfigBuilder│
│ + Build(): StorageConfig  ← validates + creates  │
└─────────────────────┬───────────────────────────┘
                      │ creates (validated)
                      ▼
┌─────────────────────────────────────────────────┐
│              StorageConfig                       │
│              (Immutable Product)                 │
├─────────────────────────────────────────────────┤
│ + Provider: string         {get;}               │
│ + BucketName: string       {get;}               │
│ + Region: string           {get;}               │
│ + MaxRetries: int          {get;}               │
│ + TimeoutSeconds: int      {get;}               │
│ + EnableEncryption: bool   {get;}               │
│ + EncryptionKey: string?   {get;}               │
│ + EnableVersioning: bool   {get;}               │
│ + EnableLogging: bool      {get;}               │
│ + LogPath: string?         {get;}               │
│ + MaxFileSizeBytes: long   {get;}               │
│ + AllowedExtensions: string[] {get;}            │
├─────────────────────────────────────────────────┤
│ ~ StorageConfig(...)  ← internal constructor    │
│ + PrintConfig()                                 │
└─────────────────────────────────────────────────┘

Usage flow:
  new StorageConfigBuilder("s3", "bucket")   ← required params
      .WithRegion("us-west-2")               ← optional
      .WithEncryption("key")                 ← optional
      .WithVersioning()                      ← optional
      .Build()                               ← validate + create
      → StorageConfig (immutable)
```

---

## V1 — Why Do We Need Builder?

**Scenario:** A `StorageConfig` with 12 parameters — some required, some optional.

**The Telescoping Constructor Problem:**

```csharp
public class StorageConfig
{
    public StorageConfig(
        string provider,
        string bucketName,
        string region,
        int maxRetries,
        int timeoutSeconds,
        bool enableEncryption,
        string? encryptionKey,
        bool enableVersioning,
        bool enableLogging,
        string? logPath,
        long maxFileSizeBytes,
        string[] allowedExtensions)
    { ... }
}
```

**The call site is unreadable:**

```csharp
var config = new StorageConfig(
    "s3",                      // provider
    "my-documents-bucket",     // bucketName
    "us-east-1",              // region
    3,                         // maxRetries - or is it timeout?
    30,                        // timeoutSeconds - or is it retries?
    true,                      // enableEncryption - or versioning?
    "AES-256-key-here",       // encryptionKey - or logPath?
    true,                      // enableVersioning
    true,                      // enableLogging
    "/var/logs/storage.log",  // logPath
    104857600,                 // maxFileSizeBytes
    new[] { ".pdf", ".docx" } // allowedExtensions
);
```

**What's wrong:**

| Problem | Explanation |
|---------|-------------|
| Positional ambiguity | `true, true, true` — which is encryption? versioning? logging? |
| Null parade | Simple local storage still requires null for encryptionKey, logPath, etc. |
| No validation | Can set `enableEncryption=true` with `encryptionKey=null` — invalid state |
| No defaults | Must specify ALL 12 params even when you only care about 2 |
| Overload explosion | Supporting optional params via overloads = 2^n combinations |
| Fragile | Adding a new parameter breaks all existing callers |

**The "simple config" pain:**

```csharp
// I just want local storage with defaults — but must provide EVERYTHING
var simple = new StorageConfig(
    "local", "/tmp/files", "", 3, 30, false, null,
    false, false, null, long.MaxValue, Array.Empty<string>()
);
```

---

## V2 — How to Implement Builder (Fluent)

**Step 1: The Product (immutable, internal constructor)**

```csharp
public class StorageConfig
{
    public string Provider { get; }
    public string BucketName { get; }
    public string Region { get; }
    public int MaxRetries { get; }
    public int TimeoutSeconds { get; }
    public bool EnableEncryption { get; }
    public string? EncryptionKey { get; }
    public bool EnableVersioning { get; }
    public bool EnableLogging { get; }
    public string? LogPath { get; }
    public long MaxFileSizeBytes { get; }
    public string[] AllowedExtensions { get; }

    // Only the builder can call this
    internal StorageConfig(...) { ... }
}
```

**Step 2: The Fluent Builder**

```csharp
public class StorageConfigBuilder
{
    // Required (set in constructor)
    private readonly string _provider;
    private readonly string _bucketName;

    // Optional with sensible defaults
    private string _region = "us-east-1";
    private int _maxRetries = 3;
    private int _timeoutSeconds = 30;
    private bool _enableEncryption = false;
    private string? _encryptionKey = null;
    private bool _enableVersioning = false;
    private bool _enableLogging = false;
    private string? _logPath = null;
    private long _maxFileSizeBytes = long.MaxValue;
    private string[] _allowedExtensions = Array.Empty<string>();

    // Constructor takes only REQUIRED parameters
    public StorageConfigBuilder(string provider, string bucketName)
    {
        _provider = provider;
        _bucketName = bucketName;
    }

    // Each method returns 'this' for fluent chaining
    public StorageConfigBuilder WithRegion(string region)
    {
        _region = region;
        return this;
    }

    public StorageConfigBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    public StorageConfigBuilder WithTimeout(int timeoutSeconds)
    {
        _timeoutSeconds = timeoutSeconds;
        return this;
    }

    public StorageConfigBuilder WithEncryption(string encryptionKey)
    {
        _enableEncryption = true;  // Automatically enable when key is provided
        _encryptionKey = encryptionKey;
        return this;
    }

    public StorageConfigBuilder WithVersioning()
    {
        _enableVersioning = true;
        return this;
    }

    public StorageConfigBuilder WithLogging(string logPath)
    {
        _enableLogging = true;  // Automatically enable when path is provided
        _logPath = logPath;
        return this;
    }

    public StorageConfigBuilder WithMaxFileSize(long maxFileSizeBytes)
    {
        _maxFileSizeBytes = maxFileSizeBytes;
        return this;
    }

    public StorageConfigBuilder WithAllowedExtensions(params string[] extensions)
    {
        _allowedExtensions = extensions;
        return this;
    }

    // Build() validates and creates the immutable product
    public StorageConfig Build()
    {
        if (string.IsNullOrWhiteSpace(_provider))
            throw new InvalidOperationException("Provider is required.");

        if (string.IsNullOrWhiteSpace(_bucketName))
            throw new InvalidOperationException("Bucket name is required.");

        if (_enableEncryption && string.IsNullOrWhiteSpace(_encryptionKey))
            throw new InvalidOperationException(
                "Encryption key is required when encryption is enabled.");

        if (_enableLogging && string.IsNullOrWhiteSpace(_logPath))
            throw new InvalidOperationException(
                "Log path is required when logging is enabled.");

        return new StorageConfig(...);
    }
}
```

**Step 3: Usage — reads like English**

```csharp
// Full-featured S3 config
var s3Config = new StorageConfigBuilder("s3", "my-documents-bucket")
    .WithRegion("us-west-2")
    .WithMaxRetries(5)
    .WithTimeout(60)
    .WithEncryption("AES-256-my-secret-key")
    .WithVersioning()
    .WithLogging("/var/logs/storage.log")
    .WithMaxFileSize(104857600)
    .WithAllowedExtensions(".pdf", ".docx", ".xlsx")
    .Build();

// Simple local storage — just required params + defaults
var localConfig = new StorageConfigBuilder("local", "/tmp/files")
    .Build();

// Azure with selective options — no null parade
var azureConfig = new StorageConfigBuilder("azure", "my-container")
    .WithRegion("westeurope")
    .WithEncryption("AES-256-azure-key")
    .WithMaxFileSize(52428800)
    .Build();
```

**Validation catches invalid state:**

```csharp
// This throws at Build() — encryption enabled without a valid key
var invalid = new StorageConfigBuilder("s3", "bucket")
    .WithEncryption("")  // empty key
    .Build();
// → InvalidOperationException: "Encryption key is required when encryption is enabled."
```

---

## When to Use Builder

### Use Builder When:

| Scenario | Why Builder Helps |
|----------|-------------------|
| Object has many constructor parameters (4+) | Eliminates positional ambiguity |
| Many parameters are optional with defaults | Only set what you need |
| Valid object requires cross-field validation | `Build()` enforces business rules |
| Object should be immutable after creation | Builder accumulates state, product is read-only |
| Same construction process creates different representations | Different builders, same interface |
| Object creation requires multiple steps in specific order | Builder guides the sequence |

### Don't Use Builder When:

| Scenario | Why Not |
|----------|---------|
| Object has 1-3 simple parameters | A constructor is fine |
| All parameters are required with no defaults | Builder adds indirection without benefit |
| Object is mutable (setters available) | Just set properties directly |
| No validation needed | Constructor + object initializer is simpler |

### Builder vs Constructor vs Object Initializer:

```csharp
// Constructor — fine for 1-3 required params
var repo = new S3FileRepository("bucket", "us-east-1");

// Object initializer — fine for simple mutable objects
var options = new RetryOptions { MaxRetries = 3, Timeout = 30 };

// Builder — for complex objects with validation + immutability
var config = new StorageConfigBuilder("s3", "bucket")
    .WithEncryption("key")
    .WithVersioning()
    .Build();
```

### Real-World .NET Examples:

| Builder | What It Builds |
|---------|----------------|
| `StringBuilder` | String (avoids repeated allocation) |
| `HostBuilder` / `WebApplicationBuilder` | ASP.NET Core application host |
| `ConfigurationBuilder` | IConfiguration from multiple sources |
| `HttpRequestMessage` (via `HttpRequestBuilder`) | HTTP requests |
| `IServiceCollection` | DI container registration |
| `ConnectionStringBuilder` | Database connection strings |

---

## Comparison: Before and After

```
BEFORE (V1):
new StorageConfig("s3", "bucket", "us-east-1", 3, 30, true, "key", true, true, "/log", 100, exts)
                   ↑       ↑          ↑       ↑   ↑    ↑     ↑     ↑     ↑      ↑     ↑    ↑
                   What do these mean?! Which bool is which?!

AFTER (V2):
new StorageConfigBuilder("s3", "bucket")
    .WithRegion("us-east-1")
    .WithMaxRetries(3)
    .WithTimeout(30)
    .WithEncryption("key")         ← automatically enables encryption
    .WithVersioning()
    .WithLogging("/log")           ← automatically enables logging
    .WithMaxFileSize(100)
    .WithAllowedExtensions(exts)
    .Build()                       ← validates everything
```

Every parameter is self-documenting. No guessing. No invalid state.
