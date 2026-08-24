# Chain of Responsibility Design Pattern

## Table of Contents

- [What is the Chain of Responsibility Pattern?](#what-is-the-chain-of-responsibility-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Chain of Responsibility?](#v1--why-do-we-need-chain-of-responsibility)
- [V2 — How to Implement Chain of Responsibility](#v2--how-to-implement-chain-of-responsibility)
- [Flavours of Chain of Responsibility](#flavours-of-chain-of-responsibility)
  - [Flavour 1: Pipeline / All-Run](#flavour-1-pipeline--all-run)
  - [Flavour 2: First Match Wins](#flavour-2-first-match-wins)
  - [Flavour 3: Skip-If-Irrelevant + Short-Circuit on Failure](#flavour-3-skip-if-irrelevant--short-circuit-on-failure)
- [When to Use Chain of Responsibility](#when-to-use-chain-of-responsibility)

---

## What is the Chain of Responsibility Pattern?

The Chain of Responsibility is a **behavioral design pattern** that lets you pass a request along a chain of handlers. Each handler decides either to process the request and stop, or to pass it to the next handler in the chain.

**Core Idea:**
- A request enters the chain at the first handler
- Each handler inspects the request and either:
  - Handles it (rejects or processes) and stops the chain
  - Passes it to the next handler
- The chain is configurable — you can add, remove, reorder, or skip handlers
- Each handler has a single responsibility — one validation or one action

**Key Benefits:**
- Decouples the sender of a request from its receivers
- Handlers are independent and composable
- The chain can be built differently for different scenarios

---

## UML Diagram

```
┌──────────────────────────────────────────┐
│       «interface» IUploadHandler         │
├──────────────────────────────────────────┤
│ + SetNext(next: IUploadHandler)          │
│     : IUploadHandler                     │
│ + Handle(request: UploadRequest): bool   │
└──────────────────┬───────────────────────┘
                   │ implements
                   ▼
┌──────────────────────────────────────────┐
│         BaseUploadHandler                │
│         (abstract)                       │
├──────────────────────────────────────────┤
│ - _next: IUploadHandler?                 │
├──────────────────────────────────────────┤
│ + SetNext(next): IUploadHandler          │
│ + Handle(request): bool                  │
│   → if _next != null: _next.Handle()     │
│   → else: return true (end of chain)     │
└──────────────────┬───────────────────────┘
                   │ extends
    ┌──────────────┼──────────────┬──────────────┬──────────────┐
    │              │              │              │              │
    ▼              ▼              ▼              ▼              ▼
┌────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│  Auth  │  │FileSize  │  │Extension │  │VirusScan │  │Duplicate │
│Handler │  │ Handler  │  │ Handler  │  │ Handler  │  │  Handler │
├────────┤  ├──────────┤  ├──────────┤  ├──────────┤  ├──────────┤
│Handle()│  │ Handle() │  │ Handle() │  │ Handle() │  │ Handle() │
│check   │  │ check    │  │ check    │  │ scan     │  │ check    │
│role    │  │ size     │  │ .ext     │  │ content  │  │ exists?  │
└───┬────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘  └──────────┘
    │             │             │             │
    └─────────────┴─────────────┴─────────────┘
              passes to next (or stops chain)

Request flow:
  UploadRequest → Auth → Size → Extension → VirusScan → Duplicate → APPROVED
                   │       │       │            │            │
                   ▼       ▼       ▼            ▼            ▼
              REJECTED REJECTED REJECTED   REJECTED    REJECTED
              (stops)  (stops)  (stops)    (stops)     (stops)
```

---

## V1 — Why Do We Need Chain of Responsibility?

**Scenario:** File upload validation requires multiple checks — authentication, file size, extension, virus scan, and duplicate detection.

**Without Chain of Responsibility — all logic in one method:**

```csharp
public bool Upload(UploadRequest request)
{
    // Step 1: Auth
    if (request.UserRole != "writer" && request.UserRole != "admin")
    {
        Console.WriteLine("[REJECTED] User cannot upload");
        return false;
    }

    // Step 2: Size
    if (request.Content.Length > request.MaxAllowedSizeBytes)
    {
        Console.WriteLine("[REJECTED] File too large");
        return false;
    }

    // Step 3: Extension
    var ext = Path.GetExtension(request.FileName);
    if (!request.AllowedExtensions.Contains(ext))
    {
        Console.WriteLine("[REJECTED] Extension not allowed");
        return false;
    }

    // Step 4: Virus scan
    if (!ScanForViruses(request.Content))
    {
        Console.WriteLine("[REJECTED] Malware detected");
        return false;
    }

    // Step 5: Duplicate check
    if (FileExists(request.FileName))
    {
        Console.WriteLine("[REJECTED] Duplicate");
        return false;
    }

    // All passed
    Upload(request);
    return true;
}
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| SRP violation | One method handles 5 different concerns |
| OCP violation | Adding rate-limiting = modifying this method |
| Not reusable | Can't reuse "size check" in a download workflow |
| Not configurable | Can't skip virus scan for trusted internal uploads |
| Hard to test | Must test all 5 validations through one method |
| Rigid ordering | Can't reorder checks without restructuring the method |
| Grows forever | Each new validation makes the method longer |

---

## V2 — How to Implement Chain of Responsibility

**Step 1: Define the Handler interface**

```csharp
public interface IUploadHandler
{
    IUploadHandler SetNext(IUploadHandler next);
    bool Handle(UploadRequest request);
}
```

**Step 2: Create the Base Handler (chain linking logic)**

```csharp
public abstract class BaseUploadHandler : IUploadHandler
{
    private IUploadHandler? _next;

    public IUploadHandler SetNext(IUploadHandler next)
    {
        _next = next;
        return next; // fluent chaining
    }

    public virtual bool Handle(UploadRequest request)
    {
        if (_next != null)
            return _next.Handle(request); // pass to next
        return true; // end of chain — approved
    }
}
```

**Step 3: Create concrete handlers (one per concern)**

```csharp
public class AuthenticationHandler : BaseUploadHandler
{
    public override bool Handle(UploadRequest request)
    {
        if (request.UserRole != "writer" && request.UserRole != "admin")
        {
            Console.WriteLine("[Auth] REJECTED");
            return false; // STOP the chain
        }
        Console.WriteLine("[Auth] PASSED");
        return base.Handle(request); // PASS to next handler
    }
}

public class FileSizeHandler : BaseUploadHandler
{
    public override bool Handle(UploadRequest request)
    {
        if (request.Content.Length > request.MaxAllowedSizeBytes)
        {
            Console.WriteLine("[Size] REJECTED");
            return false;
        }
        Console.WriteLine("[Size] PASSED");
        return base.Handle(request);
    }
}

public class ExtensionHandler : BaseUploadHandler { ... }
public class VirusScanHandler : BaseUploadHandler { ... }
public class DuplicateCheckHandler : BaseUploadHandler { ... }
```

**Step 4: Build and use the chain**

```csharp
// Build chain: Auth → Size → Extension → VirusScan → Duplicate
var auth = new AuthenticationHandler();
var size = new FileSizeHandler();
var extension = new ExtensionHandler();
var virusScan = new VirusScanHandler();
var duplicate = new DuplicateCheckHandler();

auth.SetNext(size).SetNext(extension).SetNext(virusScan).SetNext(duplicate);

// Start the chain
bool result = auth.Handle(new UploadRequest
{
    FileName = "report.pdf",
    Content = new byte[1024],
    UserRole = "writer"
});
// Output: Auth PASSED → Size PASSED → Extension PASSED → VirusScan PASSED → Duplicate PASSED
```

**Step 5: Different chains for different scenarios**

```csharp
// Full chain for external uploads
auth.SetNext(size).SetNext(extension).SetNext(virusScan).SetNext(duplicate);

// Shorter chain for trusted internal uploads (no virus scan, no duplicate check)
auth.SetNext(size).SetNext(extension);

// Admin-only chain (skip auth, just validate content)
size.SetNext(extension).SetNext(virusScan);
```

---

## Flavours of Chain of Responsibility

The Chain of Responsibility pattern has several variants depending on how handlers interact with the chain. The abstract base class changes significantly between each flavour.

---

### Flavour 1: Pipeline / All-Run

Every handler in the chain executes in sequence. A handler can **reject** (return false) to short-circuit, or **pass forward** to let the next handler run. If no handler rejects, the request is approved.

**When to use:** Validation pipelines where every check must pass — like file upload validation (auth, size, extension, virus scan, duplicate).

**Abstract base class:**

```csharp
public abstract class BaseUploadHandler : IUploadHandler
{
    private IUploadHandler? _next;

    public IUploadHandler SetNext(IUploadHandler next)
    {
        _next = next;
        return next;
    }

    /// <summary>
    /// Override in concrete handlers.
    /// Return false to reject (short-circuit).
    /// Return base.Handle(request) to pass to the next handler.
    /// </summary>
    public virtual bool Handle(UploadRequest request)
    {
        // Default: pass to next handler
        if (_next != null)
            return _next.Handle(request);

        return true; // end of chain — all handlers passed
    }
}
```

**Concrete handler:**

```csharp
public class FileSizeHandler : BaseUploadHandler
{
    public override bool Handle(UploadRequest request)
    {
        if (request.Content.Length > request.MaxAllowedSizeBytes)
        {
            Console.WriteLine("[Size] REJECTED");
            return false; // short-circuit — chain stops here
        }

        Console.WriteLine("[Size] PASSED");
        return base.Handle(request); // pass to next handler
    }
}
```

**Flow:**

```
Request → Auth → Size → Extension → VirusScan → Duplicate → APPROVED
                  │
                REJECTED (return false — chain stops, remaining handlers never run)
```

**Characteristics:**
- Every handler runs unless a previous handler rejected
- No `CanHandle()` — every handler is always relevant
- Short-circuit on `return false`
- Simplest implementation

---

### Flavour 2: First Match Wins (Classic GoF)

The request travels down the chain until **one handler claims it**. The first handler whose `CanHandle()` returns true takes full ownership. Only that one handler processes it — the rest are skipped.

**When to use:** Command routing, event handling, support ticket escalation — "which handler should own this request?"

**Abstract base class:**

```csharp
public abstract class BaseUploadHandler : IUploadHandler
{
    private IUploadHandler? _next;

    public IUploadHandler SetNext(IUploadHandler next)
    {
        _next = next;
        return next;
    }

    /// <summary>
    /// Determines if this handler is the RIGHT one for this request.
    /// Only the first matching handler processes the request.
    /// </summary>
    protected abstract bool CanHandle(UploadRequest request);

    /// <summary>
    /// The actual processing — only called if CanHandle() returned true.
    /// This handler takes FULL ownership. Chain does NOT continue after this.
    /// </summary>
    protected abstract bool Process(UploadRequest request);

    public bool Handle(UploadRequest request)
    {
        if (CanHandle(request))
        {
            // This handler claims the request — process it and STOP
            return Process(request);
            // Chain does NOT continue — this handler owns it
        }

        // This handler can't handle it — pass to next
        if (_next != null)
            return _next.Handle(request);

        // No handler could handle the request
        throw new InvalidOperationException(
            $"No handler in the chain could process request: {request.FileName}");
    }
}
```

**Concrete handlers:**

```csharp
public class SmallFileHandler : BaseUploadHandler
{
    protected override bool CanHandle(UploadRequest request)
        => request.Content.Length < 1024 * 1024; // < 1MB

    protected override bool Process(UploadRequest request)
    {
        Console.WriteLine($"[SmallFile] Handling '{request.FileName}' via fast path (sync upload)");
        return true;
    }
}

public class LargeFileHandler : BaseUploadHandler
{
    protected override bool CanHandle(UploadRequest request)
        => request.Content.Length >= 1024 * 1024; // >= 1MB

    protected override bool Process(UploadRequest request)
    {
        Console.WriteLine($"[LargeFile] Handling '{request.FileName}' via multipart upload");
        return true;
    }
}
```

**Flow:**

```
Request → SmallFileHandler → LargeFileHandler → FallbackHandler
               │
          CanHandle? YES → Process() → DONE (chain stops)
          CanHandle? NO  → pass to next handler
```

**Characteristics:**
- Only ONE handler processes each request
- Once a handler claims it (`CanHandle` = true), the chain stops
- Remaining handlers never see the request
- Throws if no handler can handle (or use a fallback handler at the end)
- Similar to strategy pattern, but with fallback/escalation semantics

---

### Flavour 3: Skip-If-Irrelevant + Short-Circuit on Failure

Handlers that **can't handle** are silently skipped. Handlers that **can handle** process the request — if they succeed, the chain continues to the next relevant handler. If they **fail** (return false or throw an exception), the chain stops immediately.

**When to use:** Conditional validation where some checks only apply to certain requests — e.g., virus scan only for external uploads, duplicate check only in non-overwrite mode, encryption validation only for sensitive files.

**Abstract base class:**

```csharp
public abstract class BaseUploadHandler : IUploadHandler
{
    private IUploadHandler? _next;

    public IUploadHandler SetNext(IUploadHandler next)
    {
        _next = next;
        return next;
    }

    /// <summary>
    /// Override to define WHEN this handler should run.
    /// Return false to skip this handler entirely (not a rejection — just irrelevant).
    /// </summary>
    protected abstract bool CanHandle(UploadRequest request);

    /// <summary>
    /// The actual validation/processing logic.
    /// Return true = passed, chain continues.
    /// Return false = rejected, chain stops (short-circuit).
    /// Throw exception = failure, chain stops (short-circuit).
    /// </summary>
    protected abstract bool Process(UploadRequest request);

    public bool Handle(UploadRequest request)
    {
        if (CanHandle(request))
        {
            try
            {
                bool passed = Process(request);
                if (!passed)
                {
                    // Handler rejected the request — short-circuit
                    Console.WriteLine($"  [Chain] Short-circuited: rejected by {GetType().Name}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Exception = immediate short-circuit
                Console.WriteLine($"  [Chain] Short-circuited: {GetType().Name} threw: {ex.Message}");
                return false;
            }
        }
        // CanHandle == false → skip this handler silently (not a rejection!)

        // Continue to next handler in chain
        if (_next != null)
            return _next.Handle(request);

        return true; // end of chain — all relevant handlers passed
    }
}
```

**Concrete handlers:**

```csharp
public class VirusScanHandler : BaseUploadHandler
{
    protected override bool CanHandle(UploadRequest request)
    {
        // Skip virus scan for admin (trusted) uploads
        return request.UserRole != "admin";
    }

    protected override bool Process(UploadRequest request)
    {
        Console.WriteLine($"  [VirusScan] Scanning '{request.FileName}'...");

        if (IsInfected(request.Content))
            throw new InvalidOperationException($"Malware detected in '{request.FileName}'!");

        Console.WriteLine($"  [VirusScan] Clean");
        return true;
    }

    private bool IsInfected(byte[] content) => false;
}

public class DuplicateCheckHandler : BaseUploadHandler
{
    private readonly bool _allowOverwrite;

    public DuplicateCheckHandler(bool allowOverwrite = false)
    {
        _allowOverwrite = allowOverwrite;
    }

    protected override bool CanHandle(UploadRequest request)
    {
        // Skip duplicate check if overwrite mode is enabled
        return !_allowOverwrite;
    }

    protected override bool Process(UploadRequest request)
    {
        if (FileExists(request.FileName))
        {
            Console.WriteLine($"  [Duplicate] '{request.FileName}' already exists");
            return false; // rejection — short-circuit
        }

        Console.WriteLine($"  [Duplicate] No duplicate found");
        return true;
    }

    private bool FileExists(string fileName) => false;
}

public class FileSizeHandler : BaseUploadHandler
{
    protected override bool CanHandle(UploadRequest request)
    {
        // Always relevant — every upload needs size validation
        return true;
    }

    protected override bool Process(UploadRequest request)
    {
        if (request.Content.Length > request.MaxAllowedSizeBytes)
        {
            Console.WriteLine($"  [Size] {request.Content.Length} > {request.MaxAllowedSizeBytes}");
            return false;
        }

        Console.WriteLine($"  [Size] OK");
        return true;
    }
}
```

**Flow:**

```
Request (UserRole=admin) → Auth → Size → Extension → VirusScan → Duplicate → APPROVED
                                                         │
                                                    CanHandle? NO
                                                    (admin = trusted)
                                                       SKIP ↓
                                                    (continue to next)

Request (UserRole=reader) → Auth → ...
                             │
                        CanHandle? YES → Process() → return false → SHORT-CIRCUIT
```

**Characteristics:**
- `CanHandle() = false` → handler is **skipped** (not a rejection, just irrelevant)
- `Process() returns false` → request is **rejected** (short-circuit)
- `Process() throws` → request is **failed** (short-circuit)
- `Process() returns true` → continue to next handler
- Same chain works for different request types — handlers self-select

---

### Flavours Comparison

| Aspect | Pipeline / All-Run | First Match Wins | Skip + Short-Circuit |
|--------|-------------------|-----------------|---------------------|
| Handlers that run | All (until rejection) | Only one (the first match) | Only relevant ones (CanHandle = true) |
| `CanHandle()`? | No | Yes (determines ownership) | Yes (determines relevance) |
| After match/handle | Chain continues | Chain STOPS | Chain continues |
| Short-circuit | On rejection (return false) | Always (after first match) | On rejection or exception |
| Skipping | Not possible | Implicit (CanHandle = false) | Explicit (CanHandle = false → skip silently) |
| Use case | Validation (all must pass) | Routing (who handles this?) | Conditional validation (some checks optional) |
| ASP.NET analogy | Middleware pipeline | Controller routing | Middleware with `app.UseWhen()` |

---

## When to Use Chain of Responsibility

### Use Chain of Responsibility When:

| Scenario | Why It Helps |
|----------|--------------|
| Multiple validations in sequence | Each handler does one check, chain stops on first failure |
| Processing pipeline with optional steps | Build different chains for different scenarios |
| Request must be handled by one of several handlers | First matching handler processes it |
| Order of processing matters and may change | Reorder by rebuilding the chain |
| You need to add/remove processing steps dynamically | Add a handler without modifying others |

### Don't Use Chain of Responsibility When:

| Scenario | Why Not |
|----------|---------|
| Only one or two checks | Simple if/else is clearer |
| Every handler MUST execute (no short-circuiting) | Use Decorator or middleware instead |
| Handlers need to know about each other | Chain assumes independence |
| Performance is critical and the chain is long | Each handler is a virtual method call |

### Real-World .NET Examples:

| Example | How It Uses CoR |
|---------|-----------------|
| ASP.NET Core Middleware | Each middleware calls `next()` or short-circuits |
| `HttpMessageHandler` pipeline | `DelegatingHandler` chain for HTTP request processing |
| FluentValidation | Validators chained, each adds errors |
| Exception handling | Try/catch blocks up the call stack |
| Logging handlers | LogLevel filtering before writing |
| Event bubbling (WPF/WinForms) | Event travels up the visual tree until handled |

### Chain of Responsibility vs Decorator:

| Aspect | Chain of Responsibility | Decorator |
|--------|------------------------|-----------|
| Can stop the chain? | Yes — handler can reject/handle and stop | No — always delegates |
| All handlers execute? | No — stops at first handler that handles it | Yes — every decorator runs |
| Intent | "Who should handle this?" or "Should this be allowed?" | "Add behavior to every call" |
| Example | Validation pipeline (reject on first failure) | Logging + Caching + Retry (all execute) |

```
Chain of Responsibility:
  Auth → Size → Extension → VirusScan
    ↓       ↓       ↓          ↓
  STOP    STOP    STOP       STOP   (any handler can end it)

Decorator:
  Logging → Caching → Retry → S3FileRepository
    ↓          ↓        ↓         ↓
  (always continues to the next layer — every decorator executes)
```

---

## Bonus Example: Coin Change (ATM Dispenser)

A classic Chain of Responsibility example. An ATM needs to dispense an amount using the largest denominations first (₹500 → ₹200 → ₹100 → ₹50 → ₹20 → ₹10). Each handler dispenses as many notes of its denomination as possible, then passes the remaining amount to the next handler.

### State Machine / Flow Diagram

```
Amount: ₹1370

  ₹500 Handler          ₹200 Handler        ₹100 Handler       ₹50 Handler        ₹20 Handler       ₹10 Handler
      │                      │                    │                  │                   │                  │
  1370 ÷ 500 = 2        370 ÷ 200 = 1       170 ÷ 100 = 1      70 ÷ 50 = 1        20 ÷ 20 = 1       0 ÷ 10 = 0
  remainder: 370         remainder: 170      remainder: 70      remainder: 20      remainder: 0       DONE
      │                      │                    │                  │                   │
      └── 2 × ₹500 ──→     └── 1 × ₹200 ──→   └── 1 × ₹100 ──→ └── 1 × ₹50 ──→    └── 1 × ₹20 ──→ END
```

**Result:** 2×₹500 + 1×₹200 + 1×₹100 + 1×₹50 + 1×₹20 = ₹1370 ✓

### UML Diagram

```
┌─────────────────────────────────────────┐
│      «interface» ICurrencyHandler       │
├─────────────────────────────────────────┤
│ + SetNext(next: ICurrencyHandler)       │
│     : ICurrencyHandler                  │
│ + Dispense(amount: int): void           │
└──────────────────┬──────────────────────┘
                   │ implements
                   ▼
┌─────────────────────────────────────────┐
│         CurrencyHandler                 │
│         (concrete handler)              │
├─────────────────────────────────────────┤
│ - _denomination: int                    │
│ - _next: ICurrencyHandler?              │
├─────────────────────────────────────────┤
│ + CurrencyHandler(denomination: int)    │
│ + SetNext(next): ICurrencyHandler       │
│ + Dispense(amount): void                │
│   → dispense own notes                  │
│   → pass remainder to _next             │
└─────────────────────────────────────────┘

Chain: ₹500 → ₹200 → ₹100 → ₹50 → ₹20 → ₹10
```

### Implementation

```csharp
// ─── Handler Interface ───
public interface ICurrencyHandler
{
    ICurrencyHandler SetNext(ICurrencyHandler next);
    void Dispense(int amount);
}

// ─── Concrete Handler (generic — works for any denomination) ───
public class CurrencyHandler : ICurrencyHandler
{
    private readonly int _denomination;
    private ICurrencyHandler? _next;

    public CurrencyHandler(int denomination)
    {
        _denomination = denomination;
    }

    public ICurrencyHandler SetNext(ICurrencyHandler next)
    {
        _next = next;
        return next;
    }

    public void Dispense(int amount)
    {
        if (amount >= _denomination)
        {
            int noteCount = amount / _denomination;
            int remainder = amount % _denomination;

            Console.WriteLine($"  Dispensing {noteCount} x ₹{_denomination}");

            if (remainder > 0 && _next != null)
            {
                _next.Dispense(remainder);
            }
        }
        else
        {
            // This denomination is too large — pass to next
            _next?.Dispense(amount);
        }
    }
}

// ─── Concrete Denomination Classes (if you want explicit types per denomination) ───
public class FiveHundredHandler : CurrencyHandler
{
    public FiveHundredHandler() : base(500) { }
}

public class TwoHundredHandler : CurrencyHandler
{
    public TwoHundredHandler() : base(200) { }
}

public class HundredHandler : CurrencyHandler
{
    public HundredHandler() : base(100) { }
}

public class FiftyHandler : CurrencyHandler
{
    public FiftyHandler() : base(50) { }
}

public class TwentyHandler : CurrencyHandler
{
    public TwentyHandler() : base(20) { }
}

public class TenHandler : CurrencyHandler
{
    public TenHandler() : base(10) { }
}

// ─── ATM Dispenser (builds the chain) ───
public class ATMDispenser
{
    private readonly ICurrencyHandler _chain;

    public ATMDispenser()
    {
        // Using the concrete denomination classes
        var note500 = new FiveHundredHandler();
        var note200 = new TwoHundredHandler();
        var note100 = new HundredHandler();
        var note50 = new FiftyHandler();
        var note20 = new TwentyHandler();
        var note10 = new TenHandler();

        // Build chain: largest → smallest
        note500.SetNext(note200).SetNext(note100)
               .SetNext(note50).SetNext(note20).SetNext(note10);

        _chain = note500;
    }

    public void Withdraw(int amount)
    {
        if (amount % 10 != 0)
        {
            Console.WriteLine($"  Cannot dispense ₹{amount} — must be multiple of 10");
            return;
        }

        Console.WriteLine($"  Withdrawing ₹{amount}:");
        _chain.Dispense(amount);
    }
}
```

### Usage

```csharp
var atm = new ATMDispenser();

atm.Withdraw(1370);
// Output:
//   Withdrawing ₹1370:
//   Dispensing 2 x ₹500
//   Dispensing 1 x ₹200
//   Dispensing 1 x ₹100
//   Dispensing 1 x ₹50
//   Dispensing 1 x ₹20

atm.Withdraw(2530);
// Output:
//   Withdrawing ₹2530:
//   Dispensing 5 x ₹500
//   Dispensing 0... (skipped) → passes 30 to next
//   Dispensing 1 x ₹20
//   Dispensing 1 x ₹10

atm.Withdraw(80);
// Output:
//   Withdrawing ₹80:
//   Dispensing 1 x ₹50
//   Dispensing 1 x ₹20
//   Dispensing 1 x ₹10
```

### Why This Fits Chain of Responsibility

| Aspect | How It Applies |
|--------|----------------|
| Each handler has ONE job | Dispense notes of one denomination only |
| Handler decides to process or pass | If amount < denomination → pass to next |
| Chain order matters | Largest first ensures fewest notes |
| Partial handling | Handler dispenses what it can, passes remainder |
| Adding new denomination | Insert a new handler in the chain — no existing code changes |

This is the **Pipeline / partial-handling** variant — each handler processes what it can and passes the remainder forward, rather than stopping the chain entirely.
