using Command.V1;

// =============================================================================
// V1: WHY DO WE NEED THE COMMAND PATTERN?
// =============================================================================

Console.WriteLine("=== Without Command: Direct calls, no undo/history ===");
Console.WriteLine();

var storage = new FileStorageService();

storage.Upload("report.pdf", new byte[] { 1, 2, 3 });
storage.Upload("data.csv", new byte[] { 4, 5, 6 });
storage.Rename("data.csv", "sales-data.csv");
storage.Delete("report.pdf");
storage.ListFiles();

Console.WriteLine();
Console.WriteLine("=== Problems ===");
Console.WriteLine("1. NO UNDO: Deleted report.pdf — can't get it back");
Console.WriteLine("2. NO HISTORY: Which operations happened? In what order? No log.");
Console.WriteLine("3. NO QUEUE: Can't batch operations and execute later");
Console.WriteLine("4. NO REPLAY: Can't replay a sequence of operations (disaster recovery)");
Console.WriteLine("5. TIGHT COUPLING: Client calls storage methods directly");
Console.WriteLine("6. NO MACRO: Can't group multiple operations as one atomic action");
Console.WriteLine("7. NO DEFERRED EXECUTION: Can't schedule operations for later");
Console.WriteLine("8. NO AUDIT: Who performed what operation? When? No trace.");
