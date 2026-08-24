using Command.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT COMMAND PATTERN
// =============================================================================

Console.WriteLine("=== Command Pattern: Undo, History, Queue ===");
Console.WriteLine();

var storage = new FileStorageService();
var history = new CommandHistory();

Console.WriteLine("--- Execute commands ---");
Console.WriteLine();
history.Execute(new UploadCommand(storage, "report.pdf", new byte[] { 1, 2, 3 }));
history.Execute(new UploadCommand(storage, "data.csv", new byte[] { 4, 5, 6 }));
history.Execute(new RenameCommand(storage, "data.csv", "sales-data.csv"));
history.Execute(new DeleteCommand(storage, "report.pdf"));

Console.WriteLine();
storage.ListFiles();

Console.WriteLine();
Console.WriteLine("--- Undo last operation (restore deleted file) ---");
Console.WriteLine();
history.Undo();
storage.ListFiles();

Console.WriteLine();
Console.WriteLine("--- Undo again (reverse rename) ---");
Console.WriteLine();
history.Undo();
storage.ListFiles();

Console.WriteLine();
Console.WriteLine("--- Redo (re-apply rename) ---");
Console.WriteLine();
history.Redo();
storage.ListFiles();

Console.WriteLine();
Console.WriteLine("--- History ---");
history.PrintHistory();

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. UNDO/REDO: Every command can be reversed");
Console.WriteLine("2. HISTORY: Full log of all operations performed");
Console.WriteLine("3. QUEUE: Commands can be stored and executed later");
Console.WriteLine("4. REPLAY: Replay history for disaster recovery");
Console.WriteLine("5. DECOUPLED: Invoker doesn't know the receiver (storage)");
Console.WriteLine("6. COMPOSABLE: MacroCommand = list of commands executed together");
Console.WriteLine("7. SERIALIZABLE: Commands can be persisted to disk/DB for audit trail");
