# Template Method Design Pattern

## Table of Contents

- [What is the Template Method Pattern?](#what-is-the-template-method-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Template Method?](#v1--why-do-we-need-template-method)
- [V2 — How to Implement Template Method](#v2--how-to-implement-template-method)
- [When to Use Template Method](#when-to-use-template-method)
- [LLD Problems Where Template Method Applies](#lld-problems-where-template-method-applies)

---

## What is the Template Method Pattern?

The Template Method is a **behavioral design pattern** that defines the skeleton of an algorithm in a base class and lets subclasses override specific steps without changing the overall structure.

**Core Idea:**
- A base class defines the **workflow** (sequence of steps) in a non-virtual method
- Individual steps are declared as `abstract` (must override) or `virtual` (can override)
- Subclasses fill in the **varying details** — but CANNOT change the order or skip steps
- Common logic is implemented ONCE in the base class — shared across all subclasses

**The "Hollywood Principle":** "Don't call us, we'll call you." The base class controls the flow and calls subclass methods at the right time — subclasses don't drive the workflow.

---

## UML Diagram

```
┌──────────────────────────────────────────────────────────┐
│              BaseDataExporter (abstract)                  │
├──────────────────────────────────────────────────────────┤
│ + Export(records[])            ← TEMPLATE METHOD (final)  │
│   {                                                      │
│     Connect();                ← abstract                  │
│     if (!Validate(records))   ← virtual (default impl)   │
│       return;                                            │
│     transformed = Transform(records); ← abstract         │
│     Write(transformed);       ← abstract                  │
│     OnExportComplete(count);  ← hook (optional override) │
│     Disconnect();             ← abstract                  │
│   }                                                      │
├──────────────────────────────────────────────────────────┤
│ # Connect()            abstract                          │
│ # Validate(records)    virtual (default: check non-empty)│
│ # Transform(records)   abstract                          │
│ # Write(records)       abstract                          │
│ # OnExportComplete(n)  virtual hook (default: no-op)     │
│ # Disconnect()         abstract                          │
└────────────────────────────┬─────────────────────────────┘
                             │ extends
          ┌──────────────────┼──────────────────┐
          │                  │                  │
          ▼                  ▼                  ▼
┌────────────────┐  ┌─────────────────┐  ┌──────────────────┐
│ S3DataExporter │  │AzureBlobExporter│  │LocalFileExporter │
├────────────────┤  ├─────────────────┤  ├──────────────────┤
│# Connect()     │  │# Connect()      │  │# Connect()       │
│  IAM auth      │  │  Managed ID     │  │  mkdir -p         │
│# Transform()   │  │# Transform()    │  │# Transform()     │
│  → Parquet     │  │  → JSON         │  │  → CSV            │
│# Write()       │  │# Write()        │  │# Write()         │
│  → s3://bucket │  │  → container/   │  │  → /exports/      │
│# Disconnect()  │  │# Disconnect()   │  │# Disconnect()    │
│# OnExport..()  │  │  (uses default) │  │# Validate()      │
│  → send metrics│  │                 │  │  → allow empty    │
└────────────────┘  └─────────────────┘  └──────────────────┘

Step types:
  abstract  = subclass MUST implement (Connect, Transform, Write, Disconnect)
  virtual   = subclass CAN override with default (Validate)
  hook      = subclass CAN override, default is no-op (OnExportComplete)
  final     = Export() template method — subclass CANNOT override
```

---

## V1 — Why Do We Need Template Method?

**Scenario:** Multiple data exporters (S3, Azure, Local) all follow the same workflow: Connect → Validate → Transform → Write → Disconnect. But each has slightly different details.

**Without Template Method — duplicated structure:**

```csharp
// S3DataExporter
public void Export(string[] records)
{
    Connect();      // S3-specific
    Validate();     // IDENTICAL across all
    Transform();    // S3-specific (Parquet)
    Write();        // S3-specific
    Disconnect();   // S3-specific
}

// AzureBlobDataExporter — 90% identical!
public void Export(string[] records)
{
    Connect();      // Azure-specific
    Validate();     // IDENTICAL (copy-pasted!)
    Transform();    // Azure-specific (JSON)
    Write();        // Azure-specific
    Disconnect();   // Azure-specific
}

// LocalFileDataExporter — same pattern again!
public void Export(string[] records) { ... }
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| DRY violation | 70% of code identical across 3 classes |
| Copy-paste bugs | Fix validation in S3, forget to fix in Azure |
| No enforcement | Nothing guarantees all exporters follow the same steps |
| OCP violation | Adding a "log timing" step = modifying ALL classes |
| Inconsistency | One exporter might skip validation accidentally |

---

## V2 — How to Implement Template Method

**Step 1: Abstract base class defines the skeleton**

```csharp
public abstract class BaseDataExporter
{
    // TEMPLATE METHOD — fixed workflow, cannot be overridden
    public void Export(string[] records)
    {
        Connect();
        if (!Validate(records)) return;
        var transformed = Transform(records);
        Write(transformed);
        OnExportComplete(transformed.Length);  // hook
        Disconnect();
    }

    // Abstract: subclass MUST implement
    protected abstract void Connect();
    protected abstract string[] Transform(string[] records);
    protected abstract void Write(string[] transformedRecords);
    protected abstract void Disconnect();

    // Virtual: shared default, subclass CAN override
    protected virtual bool Validate(string[] records)
    {
        if (records.Length == 0) return false;
        return true;
    }

    // Hook: optional, default is no-op
    protected virtual void OnExportComplete(int recordCount) { }
}
```

**Step 2: Concrete subclasses provide only the varying parts**

```csharp
public class S3DataExporter : BaseDataExporter
{
    protected override void Connect()
        => Console.WriteLine("[S3] Connecting with IAM...");

    protected override string[] Transform(string[] records)
        => records.Select(r => $"PARQUET:{r}").ToArray();

    protected override void Write(string[] transformedRecords)
        => Console.WriteLine($"[S3] Writing to s3://bucket/exports/");

    protected override void Disconnect()
        => Console.WriteLine("[S3] Closing connection");

    // Override hook: add metrics
    protected override void OnExportComplete(int recordCount)
        => Console.WriteLine($"[S3] Metrics: {recordCount} records exported");
}

public class LocalFileDataExporter : BaseDataExporter
{
    protected override void Connect()
        => Console.WriteLine("[Local] Ensuring directory exists...");

    protected override string[] Transform(string[] records)
        => records.Select(r => $"\"{r}\"").ToArray();

    protected override void Write(string[] transformedRecords)
        => Console.WriteLine($"[Local] Writing to /exports/data.csv");

    protected override void Disconnect()
        => Console.WriteLine("[Local] Closing file handle");

    // Override validation: local allows empty files
    protected override bool Validate(string[] records) => true;
}
```

**Step 3: Usage — polymorphism drives the workflow**

```csharp
BaseDataExporter exporter = new S3DataExporter();
exporter.Export(records);
// Connect(S3) → Validate(base) → Transform(Parquet) → Write(S3) → OnExport(metrics) → Disconnect(S3)

exporter = new LocalFileDataExporter();
exporter.Export(records);
// Connect(local) → Validate(always true) → Transform(CSV) → Write(local) → Disconnect(local)
```

---

## When to Use Template Method

### Use Template Method When:

| Scenario | Why It Helps |
|----------|--------------|
| Multiple classes share the same workflow structure | Define skeleton once in base class |
| Steps vary in details but not in order | Subclasses override steps, not sequence |
| Common logic should be written once (DRY) | Validate/logging/timing in base class |
| You want to enforce a workflow | Subclasses can't skip or reorder steps |
| Hook points for optional behavior | Default no-op, subclasses override if needed |

### Don't Use Template Method When:

| Scenario | Why Not |
|----------|---------|
| Steps vary in order (not just details) | Use Strategy — each strategy defines its own flow |
| Only 1-2 steps differ and the rest is trivial | Simple method with a strategy for the varying part |
| You need runtime algorithm swap | Template Method is fixed at compile time (inheritance) |
| Deep inheritance hierarchy risk | More than 2 levels → consider composition (Strategy) instead |

### Template Method vs Strategy:

| Aspect | Template Method | Strategy |
|--------|-----------------|----------|
| Mechanism | Inheritance (abstract class) | Composition (interface injection) |
| What's fixed | Workflow skeleton (step order) | Nothing — strategy defines everything |
| What varies | Individual step implementations | Entire algorithm |
| Swap at runtime | No (fixed at compile time) | Yes (SetStrategy) |
| Subclass knows structure | Yes — inherits the workflow | No — strategy is self-contained |
| When to use | "Same steps, different details" | "Completely different algorithm" |

```
Template Method:
  Base class:  Connect → Validate → Transform → Write → Disconnect
  S3:          S3Connect  (base)   Parquet     S3Write  S3Disconnect
  Azure:       AzConnect  (base)   JSON        AzWrite  AzDisconnect
  (Same skeleton, different step implementations)

Strategy:
  Context uses ICompressionStrategy
    → GZipStrategy.Compress()     (entirely different logic)
    → LZ4Strategy.Compress()      (entirely different logic)
  (Different algorithms, swappable at runtime)
```

---

## LLD Problems Where Template Method Applies

| Problem | Template Method (skeleton) | Varying Steps |
|---------|---------------------------|---------------|
| **Payment Processing** | Validate → Authorize → Charge → Confirm → Notify | Auth method differs (card, UPI, wallet), charge API differs |
| **Report Generation** | FetchData → Filter → Format → Render → Export | Format (PDF/Excel/HTML), Render (charts/tables), Export (email/download) |
| **ETL Pipeline** | Extract → Validate → Transform → Load → Audit | Extract (DB/API/file), Transform (schema mapping), Load (warehouse/lake) |
| **Authentication Flow** | CollectCredentials → Validate → CreateToken → LogAttempt | OAuth, SAML, LDAP, BasicAuth — same flow, different details |
| **Order Fulfillment** | Validate → ReserveInventory → ProcessPayment → Ship → Notify | Shipping method, payment gateway, notification channel differ |
| **Document Parsing** | OpenFile → ParseHeader → ParseBody → ValidateStructure → Close | PDF, Word, CSV, XML — same lifecycle, different parse logic |
| **Game Turn Execution** | StartTurn → GetInput → ValidateMove → ApplyMove → CheckWin → EndTurn | Board game, card game, RPG — same loop, different rules |
| **CI/CD Pipeline** | Checkout → Build → Test → Package → Deploy | Build tool, test runner, deploy target differ |
| **Notification Sender** | Compose → FormatForChannel → Send → ConfirmDelivery | Email, SMS, Push, Slack — same workflow, different formatting + send API |
| **Data Migration** | ConnectSource → ReadBatch → Transform → WriteTarget → Verify | MySQL→Postgres, CSV→S3, Oracle→DynamoDB — same flow, different adapters |

### Example: Payment Processing

```csharp
public abstract class BasePaymentProcessor
{
    // Template Method
    public PaymentResult Process(PaymentRequest request)
    {
        Validate(request);
        Authorize(request);
        var result = Charge(request);
        Confirm(result);
        NotifyCustomer(request, result);
        return result;
    }

    protected virtual void Validate(PaymentRequest r) { /* common validation */ }
    protected abstract void Authorize(PaymentRequest r);
    protected abstract PaymentResult Charge(PaymentRequest r);
    protected virtual void Confirm(PaymentResult r) { /* common logging */ }
    protected virtual void NotifyCustomer(PaymentRequest req, PaymentResult res) { }
}

public class CreditCardProcessor : BasePaymentProcessor
{
    protected override void Authorize(PaymentRequest r)
        => Console.WriteLine("[Card] 3D Secure authorization...");
    protected override PaymentResult Charge(PaymentRequest r)
        => CallStripeAPI(r);
}

public class UPIProcessor : BasePaymentProcessor
{
    protected override void Authorize(PaymentRequest r)
        => Console.WriteLine("[UPI] VPA validation...");
    protected override PaymentResult Charge(PaymentRequest r)
        => CallUPIGateway(r);
}
```

### When to pick Template Method in LLD interviews:

Look for these signals in the problem statement:
1. "Multiple implementations share the same workflow"
2. "Steps are always in the same order"
3. "Some steps are common, some vary per type"
4. "New types will be added but the process doesn't change"
5. Words like "pipeline", "lifecycle", "workflow", "process" with variants
