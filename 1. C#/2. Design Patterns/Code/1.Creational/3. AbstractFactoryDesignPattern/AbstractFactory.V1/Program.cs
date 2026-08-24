using AbstractFactory.V1;

// =============================================================================
// V1: WHY DO WE NEED ABSTRACT FACTORY?
// =============================================================================
//
// Scenario: A document management service that uses multiple storage backends.
// In PRODUCTION (AWS): files → S3, metadata → DynamoDB, search → ElasticSearch
// In DEVELOPMENT (local): files → disk, metadata → SQLite, search → in-memory
//
// These form FAMILIES — you can't mix S3 files with SQLite metadata in prod
// because they assume the same infrastructure (VPC, IAM, region, etc.)
//
// Problem: Without Abstract Factory, the client must manually wire the correct
// combination for each environment — and nothing prevents accidental mixing.
// =============================================================================

Console.WriteLine("=== Without Abstract Factory: The Problem ===");
Console.WriteLine();

string environment = "production"; // from config/env variable

IFileRepository fileRepo;
IMetadataRepository metadataRepo;
ISearchIndex searchIndex;

// Client must manually ensure ALL repositories are from the same environment
if (environment == "production")
{
    fileRepo = new S3FileRepository();
    metadataRepo = new DynamoDbMetadataRepository();
    searchIndex = new ElasticSearchIndex();
}
else if (environment == "development")
{
    fileRepo = new LocalFileRepository();
    metadataRepo = new SqliteMetadataRepository();
    searchIndex = new InMemorySearchIndex();
}
else
{
    throw new ArgumentException($"Unknown environment: {environment}");
}

// Usage works...
fileRepo.Upload("report.pdf", new byte[] { 1, 2, 3 });
metadataRepo.Save("report.pdf", new Dictionary<string, string> { ["author"] = "Alice" });
searchIndex.Index("report.pdf", "quarterly sales report");

Console.WriteLine();
Console.WriteLine("=== Problems with this approach ===");
Console.WriteLine("1. Nothing prevents mixing: new S3FileRepository() + new SqliteMetadataRepository()");
Console.WriteLine("   → S3 expects AWS creds, SQLite expects local path. Inconsistent!");
Console.WriteLine("2. Every service that needs storage must repeat the same if/else block");
Console.WriteLine("3. Adding a new environment (staging, testing) = modifying every creation site");
Console.WriteLine("4. Adding a new repository type (CacheRepository) = updating every if/else");
Console.WriteLine("5. No compile-time guarantee that all repositories match the same environment");
Console.WriteLine();
Console.WriteLine("=== Using DocumentService (with if/else baked in) ===");
Console.WriteLine();

var docService = new DocumentService("production");
docService.UploadDocument("report.pdf", new byte[] { 1, 2, 3 }, "Alice");
Console.WriteLine();
docService.SearchDocuments("quarterly report");

Console.WriteLine();
Console.WriteLine("=== The dangerous mix that compiles fine but breaks at runtime ===");

// This compiles but is WRONG — mixing cloud and local
var badFileRepo = new S3FileRepository();           // expects AWS
var badMetadata = new SqliteMetadataRepository();   // expects local disk
// S3 stores file with key "report.pdf" but SQLite stores metadata locally
// → file is in cloud, metadata is local → completely disconnected!
