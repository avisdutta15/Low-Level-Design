using Mediator.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT MEDIATOR PATTERN
// =============================================================================

Console.WriteLine("=== Mediator: Central coordination, zero direct coupling ===");
Console.WriteLine();

var mediator = new StorageMediator(maxQuotaBytes: 5000);

Console.WriteLine("--- Upload (Mediator coordinates Quota + Search + Notification) ---");
Console.WriteLine();
mediator.Storage.Upload("report.pdf", new byte[1000], "Alice");

Console.WriteLine();
mediator.Storage.Upload("data.csv", new byte[2000], "Bob");

Console.WriteLine();
Console.WriteLine("--- Upload triggers quota warning (>90%) ---");
Console.WriteLine();
mediator.Storage.Upload("images.zip", new byte[1600], "Charlie");

Console.WriteLine();
Console.WriteLine("--- Upload exceeds quota → Mediator pauses uploads ---");
Console.WriteLine();
mediator.Storage.Upload("extra.pdf", new byte[500], "Dave");

Console.WriteLine();
Console.WriteLine("--- Delete frees space → Mediator resumes uploads ---");
Console.WriteLine();
mediator.Storage.Delete("data.csv", 2000);

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. ZERO DIRECT COUPLING: Components only know IStorageMediator");
Console.WriteLine("2. CENTRALIZED LOGIC: All coordination rules in one place (Mediator)");
Console.WriteLine("3. BIDIRECTIONAL: Quota can trigger Storage.PauseUploads() via Mediator");
Console.WriteLine("4. TESTABLE: Test each component with a mock mediator");
Console.WriteLine("5. SINGLE CHANGE POINT: New coordination rule = modify Mediator only");
Console.WriteLine("6. NO CIRCULAR DEPS: Components → Mediator → Components (star topology)");
