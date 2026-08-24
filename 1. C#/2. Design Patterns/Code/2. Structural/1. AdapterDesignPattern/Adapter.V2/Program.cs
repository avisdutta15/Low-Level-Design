using Adapter.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT ADAPTER PATTERN
// =============================================================================

Console.WriteLine("=== Using S3 (no adapter needed — already implements IFileRepository) ===");
Console.WriteLine();

IFileRepository s3Repo = new S3FileRepository();
var s3Service = new DocumentService(s3Repo);
s3Service.UploadDocument("report.pdf", new byte[] { 1, 2, 3 });
s3Service.DownloadDocument("report.pdf");

Console.WriteLine();
Console.WriteLine("=== Using Azure via Adapter (incompatible SDK wrapped behind IFileRepository) ===");
Console.WriteLine();

// The third-party client we don't own
var azureClient = new ThirdPartyAzureBlobClient();

// The adapter wraps it behind our interface
IFileRepository azureRepo = new AzureBlobAdapter(azureClient, "my-container");

// DocumentService doesn't know it's talking to Azure — same interface!
var azureService = new DocumentService(azureRepo);
azureService.UploadDocument("invoice.pdf", new byte[] { 4, 5, 6, 7 });
azureService.DownloadDocument("invoice.pdf");
azureService.DeleteDocument("invoice.pdf");

Console.WriteLine();
Console.WriteLine("=== The adapter translates everything transparently ===");
Console.WriteLine();
Console.WriteLine("  Upload(fileName, byte[])  -->  PutBlob(container, blob, Stream)");
Console.WriteLine("  Download(fileName): byte[]  -->  GetBlob(container, blob): Stream");
Console.WriteLine("  Delete(fileName)  -->  RemoveBlob(container, blob)");
Console.WriteLine("  Exists(fileName): bool  -->  BlobExists(container, blob): bool");

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. DocumentService is UNCHANGED — still depends on IFileRepository");
Console.WriteLine("2. ThirdPartyAzureBlobClient is UNCHANGED — we don't own it");
Console.WriteLine("3. IFileRepository is UNCHANGED — hundreds of services still work");
Console.WriteLine("4. Adapter is the ONLY new class — single point of translation");
Console.WriteLine("5. Swap providers by swapping adapters — zero client changes");
