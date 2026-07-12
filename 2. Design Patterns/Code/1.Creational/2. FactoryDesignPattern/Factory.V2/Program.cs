using Factory.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT FACTORY PATTERN (Storage/Repository Example)
// =============================================================================

Console.WriteLine("=== With Factory: Clean separation ===");
Console.WriteLine();

var factory = new FileRepositoryFactory();
var service = new DocumentService(factory, StorageType.S3);

// Client only knows about the enum — not the concrete classes
service.UploadDocument("report.pdf", new byte[] { 1, 2, 3 });
service.DownloadDocument("report.pdf");
service.DeleteDocument("report.pdf");

Console.WriteLine();
Console.WriteLine("=== Switching storage providers (just change the enum) ===");
Console.WriteLine();

var localService = new DocumentService(factory, StorageType.Local);
localService.UploadDocument("draft.txt", new byte[] { 4, 5, 6 });

Console.WriteLine();
var azureService = new DocumentService(factory, StorageType.AzureBlob);
azureService.UploadDocument("image.png", new byte[] { 7, 8, 9 });

Console.WriteLine();
Console.WriteLine("=== Using factory directly ===");
IFileRepository repo = factory.CreateRepository(StorageType.S3);
repo.Upload("direct.txt", new byte[] { 10, 11 });

Console.WriteLine();
Console.WriteLine("=== Benefits achieved ===");
Console.WriteLine("1. Client (DocumentService) has ZERO coupling to concrete classes");
Console.WriteLine("2. Adding GCSFileRepository = new class + one factory case. Client unchanged.");
Console.WriteLine("3. Factory can be injected via DI → testable with mocks");
Console.WriteLine("4. Creation logic is centralized in one place");
Console.WriteLine("5. Open/Closed Principle: open for extension, closed for modification");
