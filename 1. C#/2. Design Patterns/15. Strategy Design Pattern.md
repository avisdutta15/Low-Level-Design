# Strategy Design Pattern

## Table of Contents

- [What is the Strategy Pattern?](#what-is-the-strategy-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Strategy?](#v1--why-do-we-need-strategy)
- [V2 — How to Implement Strategy](#v2--how-to-implement-strategy)
- [When to Use Strategy](#when-to-use-strategy)

---

## What is the Strategy Pattern?

The Strategy pattern is a **behavioral design pattern** that defines a family of algorithms, encapsulates each one in its own class, and makes them interchangeable. The client (context) delegates the work to a strategy object and can swap strategies at runtime without modifying its own code.

**Core Idea:**
- Define an interface for the algorithm (e.g., `ICompressionStrategy`)
- Each concrete strategy implements one algorithm
- The context holds a reference to the strategy interface — not a concrete class
- Strategies can be swapped at runtime via a setter method
- No if/else or switch to select the algorithm — polymorphism handles it

**Key Insight:** Instead of hardcoding algorithm selection inside the class, extract each algorithm into its own class and let the client choose which one to inject.

---

## UML Diagram

```
┌─────────────────────────────────────────────┐
│           FileStorageService                 │
│              (Context)                        │
├─────────────────────────────────────────────┤
│ - _compressionStrategy: ICompressionStrategy │
├─────────────────────────────────────────────┤
│ + FileStorageService(strategy)              │
│ + SetCompressionStrategy(strategy)          │
│ + Upload(fileName, content)                 │
│   → _compressionStrategy.Compress(content)  │
│ + Download(fileName, compressed)            │
│   → _compressionStrategy.Decompress(data)   │
└──────────────────┬──────────────────────────┘
                   │ uses (delegates to)
                   ▼
┌─────────────────────────────────────────────┐
│     «interface» ICompressionStrategy        │
├─────────────────────────────────────────────┤
│ + Name: string                              │
│ + Compress(data: byte[]): byte[]            │
│ + Decompress(data: byte[]): byte[]          │
└──────────────────┬──────────────────────────┘
                   │ implements
     ┌─────────────┼──────────────┬──────────────┐
     │             │              │              │
     ▼             ▼              ▼              ▼
┌─────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐
│  GZip   │ │   Zip    │ │   LZ4    │ │    No        │
│Strategy │ │ Strategy │ │ Strategy │ │ Compression  │
├─────────┤ ├──────────┤ ├──────────┤ ├──────────────┤
│Compress │ │Compress  │ │Compress  │ │ Compress     │
│ 60%     │ │ 50%      │ │ 70%      │ │ pass-through │
│Decomp.  │ │Decomp.   │ │Decomp.   │ │ pass-through │
└─────────┘ └──────────┘ └──────────┘ └──────────────┘

Runtime swap:
  storage.SetCompressionStrategy(new LZ4CompressionStrategy());
  storage.Upload("file.bin", data); // uses LZ4 now — no code change
```

---

## V1 — Why Do We Need Strategy?

**Scenario:** A file storage service needs to compress files before uploading. Different files need different compression algorithms (GZip for logs, LZ4 for real-time data, Zip for archives, no compression for images).

**Without Strategy — if/else for every algorithm:**

```csharp
public class FileCompressor
{
    private readonly string _algorithm;

    public byte[] Compress(byte[] data)
    {
        if (_algorithm == "gzip")
        {
            // GZip logic...
            return compressed;
        }
        else if (_algorithm == "zip")
        {
            // Zip logic...
            return compressed;
        }
        else if (_algorithm == "lz4")
        {
            // LZ4 logic...
            return compressed;
        }
        else if (_algorithm == "none")
        {
            return data;
        }
        else
        {
            throw new ArgumentException($"Unknown: {_algorithm}");
        }
    }

    // Same if/else repeated in Decompress()...
}
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| OCP violation | Adding Brotli = modifying both Compress() and Decompress() |
| SRP violation | One class contains ALL algorithm logic |
| If/else growth | More algorithms = more branches in every method |
| Not testable | Can't test GZip in isolation without the whole class |
| No runtime swap | Algorithm fixed at construction |
| Magic strings | "gzip", "lz4" — no type safety |
| Code duplication | Same conditional repeated in Compress() and Decompress() |

---

## V2 — How to Implement Strategy

**Step 1: Define the Strategy interface**

```csharp
public interface ICompressionStrategy
{
    string Name { get; }
    byte[] Compress(byte[] data);
    byte[] Decompress(byte[] data);
}
```

**Step 2: Implement concrete strategies (one per algorithm)**

```csharp
public class GZipCompressionStrategy : ICompressionStrategy
{
    public string Name => "GZip";

    public byte[] Compress(byte[] data)
    {
        Console.WriteLine($"[GZip] Compressing {data.Length} bytes...");
        var compressed = new byte[(int)(data.Length * 0.6)];
        return compressed;
    }

    public byte[] Decompress(byte[] data)
    {
        Console.WriteLine($"[GZip] Decompressing...");
        return new byte[(int)(data.Length / 0.6)];
    }
}

public class LZ4CompressionStrategy : ICompressionStrategy
{
    public string Name => "LZ4";

    public byte[] Compress(byte[] data)
    {
        Console.WriteLine($"[LZ4] Compressing {data.Length} bytes (fast mode)...");
        var compressed = new byte[(int)(data.Length * 0.7)];
        return compressed;
    }

    public byte[] Decompress(byte[] data) { ... }
}

public class ZipCompressionStrategy : ICompressionStrategy { ... }
public class NoCompressionStrategy : ICompressionStrategy { ... }
```

**Step 3: Context uses the strategy (no if/else)**

```csharp
public class FileStorageService
{
    private ICompressionStrategy _compressionStrategy;

    public FileStorageService(ICompressionStrategy compressionStrategy)
    {
        _compressionStrategy = compressionStrategy;
    }

    // Swap at runtime
    public void SetCompressionStrategy(ICompressionStrategy strategy)
    {
        _compressionStrategy = strategy;
    }

    public void Upload(string fileName, byte[] content)
    {
        // Delegates to strategy — polymorphism, no if/else
        var compressed = _compressionStrategy.Compress(content);
        Console.WriteLine($"[Storage] Uploading '{fileName}' ({compressed.Length} bytes)");
    }

    public byte[] Download(string fileName, byte[] compressedData)
    {
        return _compressionStrategy.Decompress(compressedData);
    }
}
```

**Step 4: Usage — choose strategy at runtime**

```csharp
var storage = new FileStorageService(new GZipCompressionStrategy());
storage.Upload("logs.txt", data);       // uses GZip

storage.SetCompressionStrategy(new LZ4CompressionStrategy());
storage.Upload("realtime.bin", data);   // uses LZ4 now

storage.SetCompressionStrategy(new NoCompressionStrategy());
storage.Upload("photo.jpg", data);      // no compression for images
```

**Context-based strategy selection:**

```csharp
ICompressionStrategy strategy = fileName switch
{
    var f when f.EndsWith(".jpg") || f.EndsWith(".png") => new NoCompressionStrategy(),
    var f when f.EndsWith(".log") || f.EndsWith(".txt") => new GZipCompressionStrategy(),
    var f when f.EndsWith(".bin") => new LZ4CompressionStrategy(),
    _ => new ZipCompressionStrategy()
};

var storage = new FileStorageService(strategy);
storage.Upload(fileName, data);
```

**Adding a new algorithm (Brotli):**

```csharp
// 1. New class — implements existing interface
public class BrotliCompressionStrategy : ICompressionStrategy
{
    public string Name => "Brotli";
    public byte[] Compress(byte[] data) { /* Brotli logic */ }
    public byte[] Decompress(byte[] data) { /* Brotli logic */ }
}

// 2. Use it — ZERO changes to FileStorageService
storage.SetCompressionStrategy(new BrotliCompressionStrategy());
```

---

## When to Use Strategy

### Use Strategy When:

| Scenario | Why Strategy Helps |
|----------|-------------------|
| Multiple algorithms for the same task | Each algorithm is its own class — clean separation |
| Algorithm needs to be swapped at runtime | SetStrategy() changes behavior without code change |
| Avoiding if/else or switch for algorithm selection | Polymorphism replaces conditionals |
| Algorithms should be testable in isolation | Each strategy is independently unit-testable |
| Client shouldn't know algorithm internals | Context depends on interface only |
| New algorithms are added frequently | New class, zero modification to existing code |

### Don't Use Strategy When:

| Scenario | Why Not |
|----------|---------|
| Only one algorithm exists and won't change | Unnecessary abstraction |
| Algorithm logic is trivial (2-3 lines) | Interface + class overhead not justified |
| Algorithm never changes at runtime | Constructor injection via DI may be simpler |
| You have 2 algorithms with no foreseeable expansion | Simple if/else is fine |

### Strategy vs State vs Template Method:

| Aspect | Strategy | State | Template Method |
|--------|----------|-------|-----------------|
| What varies | An algorithm/behavior | Object's current state | Steps within an algorithm |
| Who controls swap | Client (externally) | State objects (internally, via transitions) | Fixed at compile time (inheritance) |
| Awareness | Strategies don't know about each other | States know about other states (transition targets) | Subclass knows abstract steps |
| Swap frequency | Set once or occasionally | Continuous transitions during lifecycle | Never — fixed hierarchy |
| Example | Compression: GZip vs LZ4 | Upload job: Pending → Validated → Completed | Template: ParseHeader → ParseBody → Validate |

### Real-World .NET Examples:

| Example | Strategy Interface | Concrete Strategies |
|---------|-------------------|-------------------|
| `IComparer<T>` | Comparison strategy | Custom comparers for different sort orders |
| `ISerializer` | Serialization format | JsonSerializer, XmlSerializer, ProtobufSerializer |
| `ILogger` + `ILoggerProvider` | Logging output | Console, File, Seq, Application Insights |
| `IDistributedCache` | Cache backend | Redis, Memcached, SQL, InMemory |
| `IFileProvider` | File access strategy | PhysicalFileProvider, EmbeddedFileProvider |
| `HttpMessageHandler` | HTTP processing | SocketsHttpHandler, MockHandler |
