using Facade.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT FACADE PATTERN
// =============================================================================

Console.WriteLine("=== With Facade: Client calls ONE simple method ===");
Console.WriteLine();

// Setup (usually done in DI container)
var facade = new DocumentStorageFacade(
    new FileStorageService(),
    new MetadataService(),
    new SearchIndexService(),
    new VirusScanService(),
    new NotificationService()
);

// Client just calls one method — the Facade handles the 5-step orchestration
Console.WriteLine("--- Upload (one call, Facade orchestrates 5 services) ---");
Console.WriteLine();
facade.UploadDocument("report.pdf", new byte[] { 1, 2, 3, 4, 5 }, "Alice", "application/pdf");

Console.WriteLine();
Console.WriteLine("--- Search (one call) ---");
Console.WriteLine();
var results = facade.SearchDocuments("quarterly report");
Console.WriteLine($"  Found {results.Count} documents");

Console.WriteLine();
Console.WriteLine("--- Download (one call) ---");
Console.WriteLine();
var data = facade.DownloadDocument("report.pdf");
Console.WriteLine($"  Received {data.Length} bytes");

Console.WriteLine();
Console.WriteLine("--- Delete (one call, Facade coordinates cleanup across all services) ---");
Console.WriteLine();
facade.DeleteDocument("report.pdf");

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. CLIENT SIMPLICITY: One method call instead of 5 coordinated steps");
Console.WriteLine("2. LOOSE COUPLING: Client depends on Facade only, not 5 services");
Console.WriteLine("3. SINGLE ORCHESTRATION POINT: Upload logic lives in ONE place");
Console.WriteLine("4. EASY TO CHANGE: Modify the process (add audit logging) in Facade only");
Console.WriteLine("5. TESTABLE: Mock the Facade to test clients, or test Facade in isolation");
Console.WriteLine("6. CORRECT ORDERING: Facade guarantees virus scan before upload, etc.");
Console.WriteLine();
Console.WriteLine("=== Note: Subsystems are still accessible directly if needed ===");
Console.WriteLine("The Facade doesn't HIDE services — it simplifies access to them.");
Console.WriteLine("Power users can still call FileStorageService directly for special cases.");
