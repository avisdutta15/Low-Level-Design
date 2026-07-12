# Command Design Pattern

## Table of Contents

- [What is the Command Pattern?](#what-is-the-command-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Command?](#v1--why-do-we-need-command)
- [V2 — How to Implement Command](#v2--how-to-implement-command)
- [When to Use Command](#when-to-use-command)
- [LLD Problems Where Command Applies](#lld-problems-where-command-applies)

---

## What is the Command Pattern?

The Command pattern is a **behavioral design pattern** that turns a request into a stand-alone object containing all information about the request. This lets you parameterize methods with different requests, queue or log requests, and support undo operations.

**Core Idea:**
- Encapsulate an operation (method call + arguments) as a **Command object**
- The Command knows its **Receiver** (the object that does the actual work)
- An **Invoker** executes commands without knowing what they do
- Commands can be stored, queued, logged, serialized, undone, and replayed

**Key Participants:**
- **Command** (ICommand) — interface with `Execute()` and `Undo()`
- **Concrete Command** (UploadCommand, DeleteCommand) — implements the operation
- **Receiver** (FileStorageService) — the actual service that performs the work
- **Invoker** (CommandHistory) — triggers commands, maintains history

---

## UML Diagram

```
┌──────────────────────────────────────┐
│          CommandHistory              │
│           (Invoker)                  │
├──────────────────────────────────────┤
│ - _undoStack: Stack<ICommand>        │
│ - _redoStack: Stack<ICommand>        │
├──────────────────────────────────────┤
│ + Execute(command: ICommand)         │
│ + Undo()                             │
│ + Redo()                             │
│ + PrintHistory()                     │
└──────────────────┬───────────────────┘
                   │ invokes
                   ▼
┌──────────────────────────────────────┐
│        «interface» ICommand          │
├──────────────────────────────────────┤
│ + Description: string                │
│ + Execute()                          │
│ + Undo()                             │
└──────────────────┬───────────────────┘
                   │ implements
     ┌─────────────┼──────────────┬──────────────┐
     │             │              │              │
     ▼             ▼              ▼              ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐
│ Upload   │ │ Delete   │ │ Rename   │ │   Macro      │
│ Command  │ │ Command  │ │ Command  │ │  Command     │
├──────────┤ ├──────────┤ ├──────────┤ ├──────────────┤
│-_storage │ │-_storage │ │-_storage │ │-_commands[]  │
│-_fileName│ │-_fileName│ │-_oldName │ │              │
│-_content │ │-_backup  │ │-_newName │ │Execute:      │
│          │ │          │ │          │ │ foreach exec │
│Execute:  │ │Execute:  │ │Execute:  │ │Undo:         │
│ Upload() │ │ backup + │ │ Rename() │ │ foreach undo │
│Undo:     │ │ Delete() │ │Undo:     │ │ (reverse)    │
│ Delete() │ │Undo:     │ │ Rename   │ │              │
│          │ │ Restore  │ │ (reverse)│ │              │
└─────┬────┘ └─────┬────┘ └─────┬────┘ └──────────────┘
      │             │             │
      └─────────────┴─────────────┘
                    │ calls
                    ▼
┌──────────────────────────────────────┐
│       FileStorageService             │
│          (Receiver)                  │
├──────────────────────────────────────┤
│ + Upload(fileName, content)          │
│ + Download(fileName): byte[]         │
│ + Delete(fileName)                   │
│ + Rename(oldName, newName)           │
└──────────────────────────────────────┘
```

---

## V1 — Why Do We Need Command?

**Scenario:** A file storage service performs upload, delete, rename operations. Without Command pattern, these are direct method calls with no way to undo, log, or replay them.

```csharp
// Direct calls — no history, no undo
storage.Upload("report.pdf", content);
storage.Rename("data.csv", "sales.csv");
storage.Delete("report.pdf");  // Gone forever! Can't undo!
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| No undo | Deleted file can't be restored |
| No history | Can't see what operations were performed |
| No queue | Can't batch operations for later execution |
| No replay | Can't replay operations for disaster recovery |
| Tight coupling | Client directly calls receiver methods |
| No audit trail | Who did what, when? No log |
| No macro | Can't group multiple operations atomically |

---

## V2 — How to Implement Command

**Step 1: Command interface**

```csharp
public interface ICommand
{
    string Description { get; }
    void Execute();
    void Undo();
}
```

**Step 2: Concrete commands (encapsulate each operation)**

```csharp
public class UploadCommand : ICommand
{
    private readonly FileStorageService _storage;
    private readonly string _fileName;
    private readonly byte[] _content;

    public string Description => $"Upload '{_fileName}'";

    public UploadCommand(FileStorageService storage, string fileName, byte[] content)
    {
        _storage = storage;
        _fileName = fileName;
        _content = content;
    }

    public void Execute() => _storage.Upload(_fileName, _content);
    public void Undo() => _storage.Delete(_fileName);
}

public class DeleteCommand : ICommand
{
    private readonly FileStorageService _storage;
    private readonly string _fileName;
    private byte[]? _backup;

    public string Description => $"Delete '{_fileName}'";

    public DeleteCommand(FileStorageService storage, string fileName)
    {
        _storage = storage;
        _fileName = fileName;
    }

    public void Execute()
    {
        _backup = _storage.Download(_fileName); // save for undo
        _storage.Delete(_fileName);
    }

    public void Undo()
    {
        if (_backup != null)
            _storage.Upload(_fileName, _backup); // restore
    }
}

public class RenameCommand : ICommand
{
    private readonly FileStorageService _storage;
    private readonly string _oldName;
    private readonly string _newName;

    public string Description => $"Rename '{_oldName}' → '{_newName}'";

    public RenameCommand(FileStorageService storage, string oldName, string newName)
    {
        _storage = storage;
        _oldName = oldName;
        _newName = newName;
    }

    public void Execute() => _storage.Rename(_oldName, _newName);
    public void Undo() => _storage.Rename(_newName, _oldName);
}
```

**Step 3: Invoker (executes commands, manages history)**

```csharp
public class CommandHistory
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    public void Execute(ICommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
    }
}
```

**Step 4: Usage**

```csharp
var storage = new FileStorageService();
var history = new CommandHistory();

history.Execute(new UploadCommand(storage, "report.pdf", content));
history.Execute(new RenameCommand(storage, "report.pdf", "final-report.pdf"));
history.Execute(new DeleteCommand(storage, "final-report.pdf"));

// Undo the delete — file restored!
history.Undo();

// Undo the rename — back to "report.pdf"
history.Undo();

// Redo — re-apply the rename
history.Redo();
```

---

## When to Use Command

### Use Command When:

| Scenario | Why Command Helps |
|----------|-------------------|
| Need undo/redo functionality | Each command stores state for reversal |
| Operations should be logged/audited | Commands are objects — serialize and store them |
| Operations need to be queued | Store commands in a queue, execute later |
| Need to replay operations | Re-execute stored command history |
| Macro operations (batch multiple as one) | MacroCommand holds a list of commands |
| Deferred execution | Create command now, execute later |
| Transaction-like behavior | Execute all or undo all on failure |

### Don't Use Command When:

| Scenario | Why Not |
|----------|---------|
| Simple one-way operations with no undo needed | Adds unnecessary complexity |
| Undo is impossible (sending an email) | Command pattern can't help |
| Operations are stateless | Nothing to store for undo |
| Direct method call is sufficient | Don't over-engineer |

### Command vs Strategy:

| Aspect | Command | Strategy |
|--------|---------|----------|
| Purpose | Encapsulate a REQUEST as an object | Encapsulate an ALGORITHM as an object |
| Contains | Action + receiver + undo state | Algorithm implementation |
| History | Yes — commands are stored/logged | No — strategy is stateless |
| Undo | Yes — core feature | Not applicable |
| When created | Per operation (new command each time) | Per algorithm choice (set once or swapped) |
| Example | UploadCommand, DeleteCommand | GZipStrategy, LZ4Strategy |

---

## LLD Problems Where Command Applies

| Problem | Commands | Undo Behavior |
|---------|----------|---------------|
| **Text Editor** | InsertChar, DeleteChar, Bold, Paste | Reverse each: delete char, insert char, unbold |
| **File Manager** | Copy, Move, Delete, Rename | Restore backup, reverse move, restore, reverse rename |
| **Transaction System** | Credit, Debit, Transfer | Reverse credit/debit, reverse transfer |
| **Drawing App** | DrawLine, DrawCircle, ChangeColor, Move | Remove shape, restore color, reverse move |
| **Smart Home** | TurnOn, TurnOff, SetTemp, LockDoor | Reverse each to previous state |
| **Database Migration** | AddColumn, DropTable, AlterType | DropColumn, CreateTable, RevertType |
| **Game (Chess)** | MovePiece, Capture, Castle, Promote | Reverse move, restore captured piece |
| **Shopping Cart** | AddItem, RemoveItem, ApplyCoupon | Remove item, restore item, remove coupon |
| **Workflow Engine** | ApproveStep, RejectStep, AssignUser | Revert approval, revert rejection |
| **Version Control** | Commit, Branch, Merge, Revert | Revert commit, delete branch |

### Example: Text Editor

```csharp
public class InsertCharCommand : ICommand
{
    private readonly Document _doc;
    private readonly int _position;
    private readonly char _char;

    public string Description => $"Insert '{_char}' at {_position}";

    public InsertCharCommand(Document doc, int position, char ch)
    {
        _doc = doc; _position = position; _char = ch;
    }

    public void Execute() => _doc.InsertAt(_position, _char);
    public void Undo() => _doc.DeleteAt(_position);
}

// Ctrl+Z triggers: history.Undo() → removes last inserted char
// Ctrl+Y triggers: history.Redo() → re-inserts the char
```

### Signals in LLD interview:

Look for these keywords:
1. "Undo" / "Redo" / "Revert"
2. "History" / "Audit trail" / "Log all operations"
3. "Queue operations" / "Execute later" / "Batch"
4. "Replay" / "Recover" / "Rollback"
5. "Macro" / "Composite operation" / "Transaction"
