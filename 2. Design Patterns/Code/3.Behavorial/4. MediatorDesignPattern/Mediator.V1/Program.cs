using Mediator.V1;

// =============================================================================
// V1: WHY DO WE NEED THE MEDIATOR PATTERN?
// =============================================================================

Console.WriteLine("=== Without Mediator: N×N direct dependencies ===");
Console.WriteLine();

var notification = new NotificationService();
var quota = new QuotaService(notification, maxBytes: 5000);
var search = new SearchIndexService();
var storage = new FileStorageService(quota, search, notification);

Console.WriteLine("--- Upload (Storage calls Quota, Search, Notification directly) ---");
Console.WriteLine();
storage.Upload("report.pdf", new byte[1000], "Alice");

Console.WriteLine();
storage.Upload("data.csv", new byte[2000], "Bob");

Console.WriteLine();
Console.WriteLine("--- Upload exceeds quota (Storage checks Quota, notifies on failure) ---");
Console.WriteLine();
storage.Upload("huge.zip", new byte[3000], "Charlie");

Console.WriteLine();
Console.WriteLine("--- Delete (Storage calls Quota to release, Search to remove, Notification) ---");
Console.WriteLine();
storage.Delete("report.pdf", 1000);

Console.WriteLine();
Console.WriteLine("=== Problems ===");
Console.WriteLine("1. N×N COUPLING: Storage→Quota, Storage→Search, Storage→Notification, Quota→Notification");
Console.WriteLine("2. CIRCULAR RISK: Quota notifies → Notification could trigger Storage → infinite loop");
Console.WriteLine("3. OCP VIOLATION: Adding CacheInvalidation = modifying Storage, Quota, or both");
Console.WriteLine("4. HARD TO TEST: Must mock Quota+Search+Notification just to test Storage");
Console.WriteLine("5. COMPLEX WIRING: Constructor requires all dependencies upfront");
Console.WriteLine("6. COORDINATION SCATTERED: Business rules (check quota before upload) spread across components");
Console.WriteLine("7. BIDIRECTIONAL: Storage needs Quota, but Quota could also need to pause Storage");
