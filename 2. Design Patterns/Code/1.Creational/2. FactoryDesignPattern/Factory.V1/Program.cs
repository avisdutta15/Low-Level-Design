using Factory.V1;

// =============================================================================
// V1: WHY DO WE NEED THE FACTORY PATTERN?
// =============================================================================
//
// Problem: Client code is tightly coupled to concrete repository implementations.
// When the client uses `new` directly, it must know the exact class to
// instantiate — this violates the Open/Closed Principle and makes the
// code rigid, hard to extend, and difficult to test.
// =============================================================================

Console.WriteLine("=== Without Factory: The Problem ===");
Console.WriteLine();

// The client must know EVERY concrete class and decide which one to create.
// If we add a new storage provider, we must modify EVERY place that creates repositories.

string storageType = "s3";

IFileRepository repository;

// This switch/if block is duplicated everywhere a repository is needed
if (storageType == "s3")
    repository = new S3FileRepository();
else if (storageType == "local")
    repository = new LocalFileRepository();
else if (storageType == "azure")
    repository = new AzureBlobFileRepository();
else
    throw new ArgumentException($"Unknown storage type: {storageType}");

repository.Upload("report.pdf", new byte[] { 1, 2, 3 });

Console.WriteLine();
Console.WriteLine("=== Problems with this approach ===");
Console.WriteLine("1. Client is tightly coupled to concrete classes (knows S3FileRepository, LocalFileRepository, etc.)");
Console.WriteLine("2. Adding a new provider (e.g., GCS) requires changing EVERY place that creates repositories");
Console.WriteLine("3. Violates Open/Closed Principle — not open for extension without modification");
Console.WriteLine("4. Violates Single Responsibility — client both creates AND uses objects");
Console.WriteLine("5. Hard to unit test — can't mock the creation logic");
Console.WriteLine("6. Conditional logic (if/switch) scattered across the codebase");
