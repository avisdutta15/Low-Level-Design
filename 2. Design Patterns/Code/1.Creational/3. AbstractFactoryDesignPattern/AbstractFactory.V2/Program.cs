using AbstractFactory.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT ABSTRACT FACTORY (Storage/Repository Example)
// =============================================================================

Console.WriteLine("=== Abstract Factory: Environment-consistent Storage ===");
Console.WriteLine();

// The only place that decides which factory to use
string environment = "production"; // from config, env variable, etc.

IStorageFactory factory = environment switch
{
    "production" => new AwsStorageFactory(),
    "development" => new LocalStorageFactory(),
    _ => throw new ArgumentException($"Unknown environment: {environment}")
};

// Client (DocumentService) only knows about IStorageFactory
var docService = new DocumentService(factory);

Console.WriteLine($"--- Environment: {environment.ToUpper()} ---");
docService.UploadDocument("report.pdf", new byte[] { 1, 2, 3 }, "Alice");
Console.WriteLine();
docService.SearchDocuments("quarterly report");
Console.WriteLine();
docService.DeleteDocument("report.pdf");

Console.WriteLine();
Console.WriteLine("--- Switching to DEVELOPMENT (just change the factory) ---");
Console.WriteLine();

var localService = new DocumentService(new LocalStorageFactory());
localService.UploadDocument("test.txt", new byte[] { 4, 5, 6 }, "Bob");
Console.WriteLine();
localService.SearchDocuments("test");

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. Impossible to mix S3 files + SQLite metadata (factory creates ALL)");
Console.WriteLine("2. Adding Azure = new AzureStorageFactory (BlobStorage + CosmosDB + CognitiveSearch)");
Console.WriteLine("3. DocumentService is unchanged — depends only on abstractions");
Console.WriteLine("4. Testable — pass a MockStorageFactory that returns in-memory fakes");
Console.WriteLine("5. Environment switch is ONE line — entire storage backend swaps");
