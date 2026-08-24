# Decorator Design Pattern

## Table of Contents

- [What is the Decorator Pattern?](#what-is-the-decorator-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Decorator?](#v1--why-do-we-need-decorator)
- [V2 — How to Implement Decorator](#v2--how-to-implement-decorator)
- [Decorator vs Adapter](#decorator-vs-adapter)
- [When to Use Decorator](#when-to-use-decorator)

---

## What is the Decorator Pattern?

The Decorator pattern is a **structural design pattern** that lets you attach new behaviors to objects by wrapping them in special objects (decorators) that implement the same interface. Decorators are composable — you can stack multiple decorators on top of each other, each adding its own behavior.

**Core Idea:**
- Both the real component and decorators implement the SAME interface
- Each decorator wraps another `IFileRepository` and adds behavior before/after delegating
- Decorators are stackable — like layers of an onion
- The client sees only the interface — doesn't know how many decorators are wrapped

**Key Difference from Inheritance:**
- Inheritance: behaviors are fixed at compile time, create class explosion
- Decorator: behaviors are composed at runtime, only N + M classes needed

---

## UML Diagram

```
┌──────────────────────────────────────────┐
│       «interface» IFileRepository        │
├──────────────────────────────────────────┤
│ + Upload(fileName, byte[] content)       │
│ + Download(fileName): byte[]             │
│ + Delete(fileName)                       │
└────────────┬─────────────────────────────┘
             │ implements
             │
   ┌─────────┼────────────────────────────────────────┐
   │         │              │              │           │
   ▼         ▼              ▼              ▼           ▼
┌────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│  S3    │ │ Logging  │ │ Caching  │ │Encryption│ │  Retry   │
│ File   │ │ Decorator│ │ Decorator│ │ Decorator│ │ Decorator│
│ Repo   │ ├──────────┤ ├──────────┤ ├──────────┤ ├──────────┤
│        │ │-_inner:  │ │-_inner:  │ │-_inner:  │ │-_inner:  │
│(Concrete│ │ IFileRepo│ │ IFileRepo│ │ IFileRepo│ │ IFileRepo│
│Component)│ └─────┬────┘ └─────┬────┘ └─────┬────┘ └─────┬────┘
└────────┘       │             │             │             │
                 └─────────────┴─────────────┴─────────────┘
                               │
                               │ wraps (delegates to _inner)
                               ▼

Composition example:

  LoggingDecorator
    └── wraps → RetryDecorator
                  └── wraps → CachingDecorator
                                └── wraps → EncryptionDecorator
                                              └── wraps → S3FileRepository
                                                           (actual storage)

Call flow for Download("file.pdf"):
  1. LoggingDecorator:   [LOG] Download started
  2. RetryDecorator:     try {
  3. CachingDecorator:   cache miss → delegate to inner
  4. EncryptionDecorator: decrypt result from inner
  5. S3FileRepository:   [S3] actual download
  4. EncryptionDecorator: return decrypted bytes
  3. CachingDecorator:   store in cache, return
  2. RetryDecorator:     } success (no retry needed)
  1. LoggingDecorator:   [LOG] Download completed in Xms
```

---

## V1 — Why Do We Need Decorator?

**Scenario:** We have `S3FileRepository`. Now we need logging, caching, encryption, retry, and metrics as optional cross-cutting concerns.

**Without Decorator — subclass explosion:**

```csharp
// For EACH combination, a separate class:
public class S3FileRepositoryWithLogging : IFileRepository { ... }
public class S3FileRepositoryWithCaching : IFileRepository { ... }
public class S3FileRepositoryWithLoggingAndCaching : IFileRepository { ... }
public class S3FileRepositoryWithLoggingAndCachingAndEncryption : IFileRepository { ... }
// ... and the same for LocalFileRepository, AzureFileRepository, etc.
```

**The math of class explosion:**
- 4 behaviors (logging, caching, encryption, retry)
- 3 providers (S3, Local, Azure)
- Combinations: 2^4 * 3 = **48 classes!**

**Problems:**

| Problem | Explanation |
|---------|-------------|
| Class explosion | 2^N combinations for N behaviors × M providers |
| Violates SRP | Storage + logging + caching all in one class |
| Not composable | Can't add/remove behaviors at runtime |
| Duplicated logic | Logging code copy-pasted across every subclass |
| Rigid | Adding a new behavior = new classes for every existing combination |
| Violates OCP | Must modify class hierarchy to add features |

---

## V2 — How to Implement Decorator

**Step 1: The interface (shared by component and all decorators)**

```csharp
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
}
```

**Step 2: The concrete component (actual storage — no extras)**

```csharp
public class S3FileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"[S3] Uploading '{fileName}' ({content.Length} bytes)");

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"[S3] Downloading '{fileName}'");
        return new byte[] { 1, 2, 3 };
    }

    public void Delete(string fileName)
        => Console.WriteLine($"[S3] Deleting '{fileName}'");
}
```

**Step 3: Decorators (each adds ONE behavior, delegates the rest)**

```csharp
public class LoggingDecorator : IFileRepository
{
    private readonly IFileRepository _inner;

    public LoggingDecorator(IFileRepository inner) => _inner = inner;

    public void Upload(string fileName, byte[] content)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[LOG] Upload started: '{fileName}'");
        _inner.Upload(fileName, content);    // delegate to inner
        Console.WriteLine($"[LOG] Upload completed in {sw.ElapsedMilliseconds}ms");
    }

    public byte[] Download(string fileName) { ... }
    public void Delete(string fileName) { ... }
}

public class CachingDecorator : IFileRepository
{
    private readonly IFileRepository _inner;
    private readonly Dictionary<string, byte[]> _cache = new();

    public CachingDecorator(IFileRepository inner) => _inner = inner;

    public byte[] Download(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var cached))
        {
            Console.WriteLine($"[CACHE] Hit for '{fileName}'");
            return cached;   // short-circuit — don't call inner
        }
        var data = _inner.Download(fileName);   // delegate to inner
        _cache[fileName] = data;
        return data;
    }

    public void Upload(string fileName, byte[] content) { ... }
    public void Delete(string fileName) { ... }
}

public class EncryptionDecorator : IFileRepository
{
    private readonly IFileRepository _inner;

    public EncryptionDecorator(IFileRepository inner) => _inner = inner;

    public void Upload(string fileName, byte[] content)
    {
        var encrypted = Encrypt(content);
        _inner.Upload(fileName, encrypted);  // store encrypted
    }

    public byte[] Download(string fileName)
    {
        var encrypted = _inner.Download(fileName);
        return Decrypt(encrypted);           // return decrypted
    }
    ...
}
```

**Step 4: Compose decorators at runtime**

```csharp
// Simple: just S3
IFileRepository repo = new S3FileRepository();

// Add logging:
IFileRepository logged = new LoggingDecorator(new S3FileRepository());

// Stack multiple:
IFileRepository fullStack =
    new LoggingDecorator(                   // 4. Log everything
        new RetryDecorator(                 // 3. Retry on failure
            new CachingDecorator(           // 2. Cache results
                new EncryptionDecorator(    // 1. Encrypt data
                    new S3FileRepository()  // 0. Actual storage
                )
            ),
            maxRetries: 3
        )
    );

// Same decorators, different provider — swap ONE line:
IFileRepository localFullStack =
    new LoggingDecorator(
        new CachingDecorator(
            new LocalFileRepository()  // ← just changed this
        )
    );
```

**The math with Decorator:**
- 4 decorator classes + 3 provider classes = **7 classes** (not 48!)
- Any combination composed at runtime

---

## Decorator vs Adapter

| Aspect | Decorator | Adapter |
|--------|-----------|---------|
| Purpose | **Add behavior** to an existing object | **Convert interface** of an existing object |
| Interface | Same as the wrapped object | Different from the wrapped object |
| Wraps | An object with the SAME interface | An object with a DIFFERENT interface |
| Client awareness | Client doesn't know decorators exist | Client doesn't know the adaptee's real interface |
| Stacking | Multiple decorators can be stacked | Typically one adapter per adaptee |
| Changes behavior | Yes — adds logging, caching, retry, etc. | No — just translates method calls |
| Example | `LoggingDecorator(S3FileRepository)` adds logging to S3 | `AzureBlobAdapter(ThirdPartyAzureClient)` makes Azure look like IFileRepository |

**Visual comparison:**

```
ADAPTER (converts incompatible interface):

  IFileRepository ←── AzureBlobAdapter ←── ThirdPartyAzureBlobClient
  [our interface]     [translates]         [different interface]
                                           PutBlob(), GetBlob(), RemoveBlob()

DECORATOR (adds behavior, same interface throughout):

  IFileRepository ←── LoggingDecorator ←── CachingDecorator ←── S3FileRepository
  [same interface]    [same interface]     [same interface]     [same interface]
                      adds logging          adds caching         actual storage
```

**Key distinction:**
- Adapter: "Make X look like Y" (interface translation)
- Decorator: "Make X do more" (behavior enhancement)

**Can they work together?**

```csharp
// Adapter makes Azure compatible, then Decorator adds behavior on top:
IFileRepository repo =
    new LoggingDecorator(                          // Decorator: adds logging
        new CachingDecorator(                      // Decorator: adds caching
            new AzureBlobAdapter(azureClient, "c") // Adapter: makes Azure compatible
        )
    );
```

---

## When to Use Decorator

### Use Decorator When:

| Scenario | Why Decorator Helps |
|----------|---------------------|
| Adding cross-cutting concerns (logging, caching, retry, metrics) | Each concern is a separate, reusable decorator |
| Behaviors need to be optional/configurable | Compose only what you need at runtime |
| Multiple providers need the same enhancements | Same decorators work with any implementation |
| You need to add behavior without modifying existing code | Open/Closed Principle |
| Behaviors should be stackable in any order | Decorators compose freely |

### Don't Use Decorator When:

| Scenario | Why Not |
|----------|---------|
| You need to change the interface | Use Adapter instead |
| Only one fixed combination of behaviors exists | Just put it all in one class |
| The decoration order matters in complex ways | Can become confusing to debug |
| You're adding 10+ decorators | Consider middleware pipeline or AOP instead |

### Real-World .NET Examples:

| Decorator | What It Wraps |
|-----------|---------------|
| `BufferedStream` | Wraps any `Stream`, adds buffering |
| `GZipStream` | Wraps any `Stream`, adds compression |
| `CryptoStream` | Wraps any `Stream`, adds encryption |
| `LoggingHttpMessageHandler` | Wraps `HttpMessageHandler`, adds request logging |
| Polly `RetryPolicy` | Wraps any call, adds retry logic |
| ASP.NET Middleware | Each middleware wraps the next, adds behavior |

**The .NET Stream class is the classic Decorator example:**

```csharp
// Stack decorators on a file stream:
Stream stream = new FileStream("data.bin", FileMode.Create);  // base
stream = new BufferedStream(stream);   // + buffering
stream = new GZipStream(stream, CompressionMode.Compress);  // + compression
stream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write);  // + encryption
```
