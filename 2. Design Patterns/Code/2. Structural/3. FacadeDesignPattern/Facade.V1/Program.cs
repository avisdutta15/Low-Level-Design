using Facade.V1;

// =============================================================================
// V1: WHY DO WE NEED THE FACADE PATTERN?
// =============================================================================
//
// Scenario: Uploading a document involves MANY subsystems:
//   1. Virus scan the content
//   2. Store the file in S3
//   3. Save metadata to a database
//   4. Index the document for search
//   5. Send notification to subscribers
//
// Without Facade, EVERY client must know about ALL these subsystems,
// call them in the correct ORDER, and handle errors across all of them.
// =============================================================================

Console.WriteLine("=== Without Facade: Client must orchestrate everything ===");
Console.WriteLine();

// The client needs to know about ALL of these services:
var fileStorage = new FileStorageService();
var metadata = new MetadataService();
var search = new SearchIndexService();
var virusScan = new VirusScanService();
var notification = new NotificationService();

// Upload a document — client must coordinate 5 services in correct order:
string fileName = "report.pdf";
byte[] content = new byte[] { 1, 2, 3, 4, 5 };
string author = "Alice";

Console.WriteLine("--- Uploading a document (client orchestrates) ---");
Console.WriteLine();

// Step 1: Virus scan
bool isSafe = virusScan.Scan(content);
if (!isSafe)
{
    Console.WriteLine("  ABORT: File is infected!");
    return;
}

// Step 2: Store the file
fileStorage.Upload(fileName, content);

// Step 3: Save metadata
metadata.SaveMetadata(fileName, author, content.Length, "application/pdf");

// Step 4: Index for search
var metadataDict = new Dictionary<string, string>
{
    ["author"] = author,
    ["contentType"] = "application/pdf"
};
search.IndexDocument(fileName, "quarterly sales report", metadataDict);

// Step 5: Notify subscribers
notification.NotifyUpload(fileName, author);

Console.WriteLine();
Console.WriteLine("--- Deleting a document (client orchestrates AGAIN) ---");
Console.WriteLine();

// Same complexity for deletion — different order, different calls
fileStorage.Delete(fileName);
metadata.DeleteMetadata(fileName);
search.RemoveFromIndex(fileName);
notification.NotifyDeletion(fileName);

Console.WriteLine();
Console.WriteLine("=== Problems with this approach ===");
Console.WriteLine("1. CLIENT COMPLEXITY: Every client must know 5 services + correct call order");
Console.WriteLine("2. TIGHT COUPLING: Client depends on FileStorage, Metadata, Search, VirusScan, Notification");
Console.WriteLine("3. DUPLICATED ORCHESTRATION: Every controller/handler repeats the same 5-step sequence");
Console.WriteLine("4. FRAGILE: Change the upload process = modify every client that uploads");
Console.WriteLine("5. HARD TO TEST: Must mock 5 services to test any client");
Console.WriteLine("6. ERROR HANDLING: Client must handle failures across all 5 services");
Console.WriteLine("7. ORDER DEPENDENCY: Virus scan BEFORE upload, metadata BEFORE index — easy to get wrong");
