using Observer.V1;

// =============================================================================
// V1: WHY DO WE NEED THE OBSERVER PATTERN?
// =============================================================================

Console.WriteLine("=== Without Observer: Tight coupling to every subscriber ===");
Console.WriteLine();

var storage = new FileStorageService(
    new LoggingService(),
    new SearchIndexService(),
    new NotificationService(),
    new AuditTrailService()
);

Console.WriteLine("--- Upload ---");
Console.WriteLine();
storage.Upload("report.pdf", new byte[] { 1, 2, 3 }, "Alice");

Console.WriteLine();
Console.WriteLine("--- Delete ---");
Console.WriteLine();
storage.Delete("report.pdf", "Alice");

Console.WriteLine();
Console.WriteLine("=== Problems ===");
Console.WriteLine("1. TIGHT COUPLING: FileStorageService knows about 4 other services");
Console.WriteLine("2. OCP VIOLATION: Adding MetricsCollector = modifying FileStorageService");
Console.WriteLine("3. SRP VIOLATION: Storage service handles upload + orchestration");
Console.WriteLine("4. RIGID: Can't add/remove subscribers at runtime");
Console.WriteLine("5. HARD TO TEST: Must mock 4 services just to test upload");
Console.WriteLine("6. FRAGILE: Forget to add notification in Delete()? Silent bug.");
Console.WriteLine("7. CONSTRUCTOR BLOAT: More subscribers = more constructor params");
