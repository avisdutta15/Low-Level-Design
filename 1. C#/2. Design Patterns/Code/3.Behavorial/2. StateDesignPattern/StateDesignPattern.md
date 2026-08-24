# State Design Pattern

## Table of Contents

- [What is the State Pattern?](#what-is-the-state-pattern)
- [UML Diagram](#uml-diagram)
- [State Machine Diagram](#state-machine-diagram)
- [V1 — Why Do We Need State?](#v1--why-do-we-need-state)
- [V2 — How to Implement State](#v2--how-to-implement-state)
- [When to Use State](#when-to-use-state)

---

## What is the State Pattern?

The State pattern is a **behavioral design pattern** that lets an object alter its behavior when its internal state changes. The object appears to change its class — each state is represented by a separate class, and the object delegates behavior to the current state object.

**Core Idea:**
- The **Context** (FileUploadJob) holds a reference to a current **State** object
- All actions (Validate, Upload, Cancel) are delegated to the current state
- Each **Concrete State** class implements behavior appropriate for that state
- States control transitions — they decide which state comes next
- The Context has zero conditional logic — no if/else on state

**Key Insight:** Instead of `if (state == "Pending") { ... } else if (state == "Validated") { ... }` scattered across every method, each state becomes its own class with its own implementation of every method.

---

## UML Diagram

```
┌─────────────────────────────────────┐
│          FileUploadJob              │
│           (Context)                  │
├─────────────────────────────────────┤
│ + FileName: string                  │
│ + Content: byte[]                   │
│ + CurrentState: IUploadState        │
│ + ErrorMessage: string?             │
├─────────────────────────────────────┤
│ + TransitionTo(state: IUploadState) │
│ + Validate()  → delegates           │
│ + Upload()    → delegates           │
│ + Cancel()    → delegates           │
│ + Retry()     → delegates           │
└──────────────────┬──────────────────┘
                   │ delegates to
                   ▼
┌─────────────────────────────────────┐
│      «interface» IUploadState       │
├─────────────────────────────────────┤
│ + Name: string                      │
│ + Validate(job: FileUploadJob)      │
│ + Upload(job: FileUploadJob)        │
│ + Cancel(job: FileUploadJob)        │
│ + Retry(job: FileUploadJob)         │
└──────────────────┬──────────────────┘
                   │ implements
     ┌─────────────┼────────────┬────────────┬─────────────┐
     │             │            │            │             │
     ▼             ▼            ▼            ▼             ▼
┌─────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Pending │ │Validated │ │Uploading │ │Completed │ │  Failed  │
│  State  │ │  State   │ │  State   │ │  State   │ │  State   │
├─────────┤ ├──────────┤ ├──────────┤ ├──────────┤ ├──────────┤
│Validate:│ │Validate: │ │Validate: │ │Validate: │ │Validate: │
│ check   │ │ "already"│ │ "can't"  │ │ "done"   │ │ "failed" │
│ size →  │ │Upload:   │ │Upload:   │ │Upload:   │ │Upload:   │
│ Valid or│ │ → Upload-│ │ "already"│ │ "done"   │ │ "failed" │
│ Failed  │ │   ing →  │ │Cancel:   │ │Cancel:   │ │Retry:    │
│Upload:  │ │   Compl. │ │ "can't"  │ │ "can't"  │ │ → Pending│
│ "valid  │ │Cancel:   │ │          │ │          │ │          │
│  first" │ │ → Cancel │ │          │ │          │ │          │
└─────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘
```

---

## State Machine Diagram

```
                    ┌───────────┐
                    │  Pending  │ ◄──────── (initial state)
                    └─────┬─────┘
                          │
              ┌───────────┼───────────┐
              │ Validate()│           │ Cancel()
              ▼           │           ▼
    ┌─────────────┐       │    ┌────────────┐
    │  Validated  │       │    │ Cancelled  │ (terminal)
    └──────┬──────┘       │    └────────────┘
           │              │
    ┌──────┼──────┐       │
    │Upload()     │Cancel()│
    ▼             ▼       │
┌──────────┐  ┌────────┐ │
│Uploading │  │Cancelled│ │
└─────┬────┘  └────────┘ │
      │                   │
      │ (completes)       │ (size too large)
      ▼                   ▼
┌──────────┐       ┌──────────┐
│Completed │       │  Failed  │
│(terminal)│       └─────┬────┘
└──────────┘             │
                         │ Retry()
                         ▼
                    ┌──────────┐
                    │ Pending  │ (loops back)
                    └──────────┘
```

---

## V1 — Why Do We Need State?

**Scenario:** A file upload job goes through states: Pending → Validated → Uploading → Completed (or Failed/Cancelled). Each action (Validate, Upload, Cancel, Retry) behaves differently depending on the current state.

**Without State Pattern — if/else in every method:**

```csharp
public void Validate()
{
    if (CurrentState == "Pending") { /* validate + transition */ }
    else if (CurrentState == "Validated") { Console.WriteLine("Already validated"); }
    else if (CurrentState == "Uploading") { Console.WriteLine("Can't validate while uploading"); }
    else if (CurrentState == "Completed") { Console.WriteLine("Already done"); }
    else if (CurrentState == "Failed") { Console.WriteLine("Can't validate failed job"); }
}

public void Upload()
{
    if (CurrentState == "Validated") { /* upload + transition */ }
    else if (CurrentState == "Pending") { Console.WriteLine("Validate first"); }
    else if (CurrentState == "Uploading") { Console.WriteLine("Already uploading"); }
    else if (CurrentState == "Completed") { Console.WriteLine("Already done"); }
    else if (CurrentState == "Failed") { Console.WriteLine("Job failed"); }
}

// Same pattern for Cancel(), Retry(), etc.
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| Massive conditionals | Every method has N branches for N states |
| Adding a state | New state = modify EVERY method |
| Scattered logic | "What can Pending do?" is spread across 4 methods |
| Violates OCP | Can't extend without modifying existing code |
| Hard to visualize | The state machine is buried in conditionals |
| Class grows with states × methods | 5 states × 4 methods = 20 branches |

---

## V2 — How to Implement State

**Step 1: Define the State interface**

```csharp
public interface IUploadState
{
    string Name { get; }
    void Validate(FileUploadJob job);
    void Upload(FileUploadJob job);
    void Cancel(FileUploadJob job);
    void Retry(FileUploadJob job);
}
```

**Step 2: Create the Context (holds pre-created state instances)**

```csharp
public class FileUploadJob
{
    public string FileName { get; }
    public byte[] Content { get; }
    public IUploadState CurrentState { get; private set; }
    public string? ErrorMessage { get; set; }

    // Pre-created state instances — NO allocations on transitions
    public PendingState PendingState { get; }
    public ValidatedState ValidatedState { get; }
    public UploadingState UploadingState { get; }
    public CompletedState CompletedState { get; }
    public FailedState FailedState { get; }
    public CancelledState CancelledState { get; }

    public FileUploadJob(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;

        // Create all states ONCE in the constructor
        PendingState = new PendingState();
        ValidatedState = new ValidatedState();
        UploadingState = new UploadingState();
        CompletedState = new CompletedState();
        FailedState = new FailedState();
        CancelledState = new CancelledState();

        CurrentState = PendingState; // initial state
    }

    public void TransitionTo(IUploadState newState)
    {
        Console.WriteLine($"  [Transition] {CurrentState.Name} → {newState.Name}");
        CurrentState = newState;
    }

    // Zero conditionals — just delegate
    public void Validate() => CurrentState.Validate(this);
    public void Upload() => CurrentState.Upload(this);
    public void Cancel() => CurrentState.Cancel(this);
    public void Retry() => CurrentState.Retry(this);
}
```

**Step 3: Implement each state**

```csharp
public class PendingState : IUploadState
{
    public string Name => "Pending";

    public void Validate(FileUploadJob job)
    {
        if (job.Content.Length > 10 * 1024 * 1024)
        {
            job.ErrorMessage = "File too large";
            job.TransitionTo(job.FailedState); // reuse existing instance
            return;
        }
        job.TransitionTo(job.ValidatedState); // reuse existing instance
    }

    public void Upload(FileUploadJob job)
        => Console.WriteLine("[Pending] Must validate first");

    public void Cancel(FileUploadJob job)
        => job.TransitionTo(job.CancelledState);

    public void Retry(FileUploadJob job)
        => Console.WriteLine("[Pending] Nothing to retry");
}

public class ValidatedState : IUploadState
{
    public string Name => "Validated";

    public void Validate(FileUploadJob job)
        => Console.WriteLine("[Validated] Already validated");

    public void Upload(FileUploadJob job)
    {
        job.TransitionTo(job.UploadingState);
        Console.WriteLine($"[Uploading] Uploading '{job.FileName}'...");
        job.TransitionTo(job.CompletedState);
    }

    public void Cancel(FileUploadJob job)
        => job.TransitionTo(job.CancelledState);

    public void Retry(FileUploadJob job)
        => Console.WriteLine("[Validated] Nothing to retry");
}

public class FailedState : IUploadState
{
    public string Name => "Failed";

    public void Validate(FileUploadJob job)
        => Console.WriteLine($"[Failed] Job failed: {job.ErrorMessage}");

    public void Upload(FileUploadJob job)
        => Console.WriteLine($"[Failed] Job failed: {job.ErrorMessage}");

    public void Cancel(FileUploadJob job)
        => Console.WriteLine("[Failed] Already terminal");

    public void Retry(FileUploadJob job)
    {
        job.ErrorMessage = null;
        job.TransitionTo(job.PendingState); // reuse existing instance
    }
}

// CompletedState, UploadingState, CancelledState follow same pattern...
```

**Key:** States reference `job.PendingState`, `job.ValidatedState`, etc. — never `new PendingState()`. State instances are created once in the context and reused across all transitions.

**Step 4: Usage**

```csharp
var job = new FileUploadJob("report.pdf", new byte[1024]);
job.Validate();  // Pending → Validated
job.Upload();    // Validated → Uploading → Completed
job.Cancel();    // "Cannot cancel completed upload"
```

**Adding a new state (e.g., "Queued"):**

```csharp
// 1. New class — implements IUploadState
public class QueuedState : IUploadState
{
    public string Name => "Queued";
    public void Validate(FileUploadJob job) => Console.WriteLine("[Queued] Already validated");
    public void Upload(FileUploadJob job) => Console.WriteLine("[Queued] Waiting in queue...");
    public void Cancel(FileUploadJob job) => job.TransitionTo(new CancelledState());
    public void Retry(FileUploadJob job) => Console.WriteLine("[Queued] Not failed");
}

// 2. Modify ValidatedState.Upload() to transition to Queued instead of Uploading
// 3. NO other existing state classes need to change
```

---

## When to Use State

### Use State When:

| Scenario | Why State Helps |
|----------|-----------------|
| Object behavior changes based on its state | Each state class encapsulates state-specific behavior |
| Many if/else or switch blocks checking state | Eliminate conditionals — polymorphism handles dispatch |
| State transitions follow specific rules | Each state controls its own valid transitions |
| States will be added in the future | New state = new class, no existing code modified |
| You want the state machine to be visible | State classes + TransitionTo = clear state diagram |

### Don't Use State When:

| Scenario | Why Not |
|----------|---------|
| Only 2-3 simple states with minimal behavior | Simple enum + switch is clearer |
| States don't affect behavior (just data tracking) | Use an enum field |
| Transitions are trivial (no rules) | State pattern adds overhead without benefit |
| You need a full state machine framework | Consider Stateless library instead |

### State vs Strategy:

| Aspect | State | Strategy |
|--------|-------|----------|
| Who controls transitions | State objects themselves | Client or Context |
| States know about each other | Yes — `PendingState` knows about `ValidatedState` | No — strategies are independent |
| Purpose | Model lifecycle with transitions | Choose algorithm at runtime |
| Changes over time | Yes — transitions happen as events occur | Typically set once |
| Example | Upload job: Pending → Validated → Completed | Sort algorithm: QuickSort vs MergeSort |

### Real-World .NET Examples:

| Example | States |
|---------|--------|
| `HttpClient` request lifecycle | Created → Sent → ResponseReceived → Completed |
| TCP Connection | Closed → Listen → SynReceived → Established → Closing |
| Order processing | Placed → Paid → Shipped → Delivered → Returned |
| CI/CD Pipeline | Queued → Building → Testing → Deploying → Deployed/Failed |
| Document workflow | Draft → Review → Approved → Published → Archived |

---

## Bonus Example: Vending Machine

A classic State pattern example. The machine has 4 states and behaves differently for each action depending on its current state.

### State Machine

```
                ┌─────────────┐
                │   Idle      │ ◄── (initial)
                └──────┬──────┘
                       │ InsertMoney()
                       ▼
                ┌─────────────┐
                │ HasMoney    │
                └──┬──────┬───┘
                   │      │ CancelAndRefund()
   SelectProduct() │      ▼
                   │  ┌─────────────┐
                   │  │   Idle      │
                   ▼  └─────────────┘
            ┌─────────────┐
            │ Dispensing   │
            └──────┬──────┘
                   │ (dispense complete)
                   ▼
            ┌─────────────┐
            │   Idle      │ (loops back)
            └─────────────┘
```

### Implementation

```csharp
// ─── State Interface ───
public interface IVendingMachineState
{
    string Name { get; }
    void InsertMoney(VendingMachine machine, decimal amount);
    void SelectProduct(VendingMachine machine, string product);
    void Dispense(VendingMachine machine);
    void CancelAndRefund(VendingMachine machine);
}

// ─── Context ───
public class VendingMachine
{
    public IVendingMachineState CurrentState { get; private set; }
    public decimal Balance { get; set; }
    public string? SelectedProduct { get; set; }

    private readonly Dictionary<string, decimal> _inventory = new()
    {
        ["Cola"] = 1.50m,
        ["Chips"] = 1.00m,
        ["Water"] = 0.75m
    };

    public VendingMachine()
    {
        CurrentState = new IdleState();
    }

    public void TransitionTo(IVendingMachineState state)
    {
        Console.WriteLine($"  [Transition] {CurrentState.Name} → {state.Name}");
        CurrentState = state;
    }

    public decimal GetPrice(string product) =>
        _inventory.TryGetValue(product, out var price) ? price : -1;

    public bool HasProduct(string product) => _inventory.ContainsKey(product);

    // Delegates — zero conditionals
    public void InsertMoney(decimal amount) => CurrentState.InsertMoney(this, amount);
    public void SelectProduct(string product) => CurrentState.SelectProduct(this, product);
    public void Dispense() => CurrentState.Dispense(this);
    public void CancelAndRefund() => CurrentState.CancelAndRefund(this);
}

// ─── Idle State ───
public class IdleState : IVendingMachineState
{
    public string Name => "Idle";

    public void InsertMoney(VendingMachine machine, decimal amount)
    {
        machine.Balance = amount;
        Console.WriteLine($"  [Idle] Inserted ${amount:F2}. Balance: ${machine.Balance:F2}");
        machine.TransitionTo(new HasMoneyState());
    }

    public void SelectProduct(VendingMachine machine, string product)
        => Console.WriteLine("  [Idle] Insert money first");

    public void Dispense(VendingMachine machine)
        => Console.WriteLine("  [Idle] Nothing to dispense");

    public void CancelAndRefund(VendingMachine machine)
        => Console.WriteLine("  [Idle] No money to refund");
}

// ─── HasMoney State ───
public class HasMoneyState : IVendingMachineState
{
    public string Name => "HasMoney";

    public void InsertMoney(VendingMachine machine, decimal amount)
    {
        machine.Balance += amount;
        Console.WriteLine($"  [HasMoney] Added ${amount:F2}. Balance: ${machine.Balance:F2}");
    }

    public void SelectProduct(VendingMachine machine, string product)
    {
        if (!machine.HasProduct(product))
        {
            Console.WriteLine($"  [HasMoney] Product '{product}' not available");
            return;
        }

        decimal price = machine.GetPrice(product);
        if (machine.Balance < price)
        {
            Console.WriteLine($"  [HasMoney] Insufficient funds. Need ${price:F2}, have ${machine.Balance:F2}");
            return;
        }

        machine.SelectedProduct = product;
        machine.Balance -= price;
        Console.WriteLine($"  [HasMoney] Selected '{product}' (${price:F2}). Change: ${machine.Balance:F2}");
        machine.TransitionTo(new DispensingState());
    }

    public void Dispense(VendingMachine machine)
        => Console.WriteLine("  [HasMoney] Select a product first");

    public void CancelAndRefund(VendingMachine machine)
    {
        Console.WriteLine($"  [HasMoney] Refunding ${machine.Balance:F2}");
        machine.Balance = 0;
        machine.TransitionTo(new IdleState());
    }
}

// ─── Dispensing State ───
public class DispensingState : IVendingMachineState
{
    public string Name => "Dispensing";

    public void InsertMoney(VendingMachine machine, decimal amount)
        => Console.WriteLine("  [Dispensing] Please wait...");

    public void SelectProduct(VendingMachine machine, string product)
        => Console.WriteLine("  [Dispensing] Please wait...");

    public void Dispense(VendingMachine machine)
    {
        Console.WriteLine($"  [Dispensing] 🎉 Dispensing '{machine.SelectedProduct}'!");

        if (machine.Balance > 0)
            Console.WriteLine($"  [Dispensing] Returning change: ${machine.Balance:F2}");

        machine.Balance = 0;
        machine.SelectedProduct = null;
        machine.TransitionTo(new IdleState());
    }

    public void CancelAndRefund(VendingMachine machine)
        => Console.WriteLine("  [Dispensing] Cannot cancel — already dispensing");
}
```

### Usage

```csharp
var vm = new VendingMachine();

// Happy path
vm.InsertMoney(2.00m);          // Idle → HasMoney
vm.SelectProduct("Cola");       // HasMoney → Dispensing (Cola costs $1.50)
vm.Dispense();                  // Dispensing → Idle (returns $0.50 change)

// Insufficient funds
vm.InsertMoney(0.50m);          // Idle → HasMoney
vm.SelectProduct("Cola");       // "Insufficient funds" (stays in HasMoney)
vm.InsertMoney(1.00m);          // adds to balance
vm.SelectProduct("Cola");       // HasMoney → Dispensing
vm.Dispense();                  // Dispensing → Idle

// Cancel and refund
vm.InsertMoney(2.00m);          // Idle → HasMoney
vm.CancelAndRefund();           // HasMoney → Idle (refunds $2.00)

// Invalid actions
vm.SelectProduct("Cola");       // "Insert money first" (Idle state)
vm.Dispense();                  // "Nothing to dispense" (Idle state)
```
