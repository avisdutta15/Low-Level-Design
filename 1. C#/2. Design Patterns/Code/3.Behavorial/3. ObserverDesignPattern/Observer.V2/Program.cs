using Observer.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT OBSERVER PATTERN
// =============================================================================

Console.WriteLine("=== Observer Pattern: Subscribe at runtime, zero coupling ===");
Console.WriteLine();

var storage = new FileStorageService();

// Subscribe observers (can be done at startup, via DI, or dynamically)
var logger = new LoggingObserver();
var search = new SearchIndexObserver();
var notification = new NotificationObserver();
var audit = new AuditTrailObserver();
var metrics = new MetricsObserver();

storage.Subscribe(logger);
storage.Subscribe(search);
storage.Subscribe(notification);
storage.Subscribe(audit);
storage.Subscribe(metrics);

Console.WriteLine("--- Upload (all 5 observers notified) ---");
Console.WriteLine();
storage.Upload("report.pdf", new byte[] { 1, 2, 3 }, "Alice");

Console.WriteLine();
Console.WriteLine("--- Upload another (metrics accumulates) ---");
Console.WriteLine();
storage.Upload("data.csv", new byte[] { 4, 5, 6, 7, 8 }, "Bob");

Console.WriteLine();
Console.WriteLine("--- Delete ---");
Console.WriteLine();
storage.Delete("report.pdf", "Alice");

Console.WriteLine();
Console.WriteLine("--- Unsubscribe notification + audit, then upload ---");
Console.WriteLine();
storage.Unsubscribe(notification);
storage.Unsubscribe(audit);
storage.Upload("notes.txt", new byte[] { 9, 10 }, "Charlie");
Console.WriteLine("  (Only logger, search, metrics notified — notification and audit unsubscribed)");

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. LOOSE COUPLING: FileStorageService knows nothing about concrete observers");
Console.WriteLine("2. OCP: Add MetricsObserver without modifying storage service");
Console.WriteLine("3. RUNTIME FLEXIBILITY: Subscribe/unsubscribe observers dynamically");
Console.WriteLine("4. SRP: Storage does storage. Logging does logging. Each observer has one job.");
Console.WriteLine("5. TESTABLE: Test storage in isolation (no observers). Test observers in isolation.");
Console.WriteLine("6. SCALABLE: 1 observer or 100 — storage service doesn't care");
