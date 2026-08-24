using Adapter.V1;

// =============================================================================
// V1: WHY DO WE NEED THE ADAPTER PATTERN?
// =============================================================================
//
// Scenario: Our app uses IFileRepository everywhere. We have an S3 implementation
// that works perfectly. Now we need to integrate a third-party Azure Blob SDK
// that has a COMPLETELY DIFFERENT interface.
//
// We can't modify:
//   - IFileRepository (hundreds of services depend on it)
//   - ThirdPartyAzureBlobClient (it's from a NuGet package we don't own)
//
// These two interfaces are INCOMPATIBLE — different method names, parameter
// types, and return types. Without an adapter, we're stuck.
// =============================================================================

Console.WriteLine("=== Works fine: S3 implements our interface ===");
Console.WriteLine();

IFileRepository s3Repo = new S3FileRepository();
var service = new DocumentService(s3Repo);
service.UploadDocument("report.pdf", new byte[] { 1, 2, 3 });
service.DownloadDocument("report.pdf");
service.DeleteDocument("report.pdf");

Console.WriteLine();
Console.WriteLine("=== Problem: Azure SDK has incompatible interface ===");
Console.WriteLine();

var azureClient = new ThirdPartyAzureBlobClient();

// We CAN'T do this — ThirdPartyAzureBlobClient does NOT implement IFileRepository:
// IFileRepository azureRepo = azureClient;  // COMPILE ERROR!
// var service2 = new DocumentService(azureClient);  // COMPILE ERROR!

Console.WriteLine("ThirdPartyAzureBlobClient methods:");
Console.WriteLine("  - PutBlob(containerName, blobPath, Stream content, contentType)");
Console.WriteLine("  - GetBlob(containerName, blobPath) -> Stream");
Console.WriteLine("  - RemoveBlob(containerName, blobPath)");
Console.WriteLine("  - BlobExists(containerName, blobPath) -> bool");
Console.WriteLine();
Console.WriteLine("Our IFileRepository methods:");
Console.WriteLine("  - Upload(fileName, byte[] content)");
Console.WriteLine("  - Download(fileName) -> byte[]");
Console.WriteLine("  - Delete(fileName)");
Console.WriteLine("  - Exists(fileName) -> bool");
Console.WriteLine();
Console.WriteLine("=== Incompatibilities ===");
Console.WriteLine("1. Method names differ (PutBlob vs Upload, GetBlob vs Download)");
Console.WriteLine("2. Parameter types differ (Stream vs byte[], requires containerName)");
Console.WriteLine("3. Return types differ (Stream vs byte[])");
Console.WriteLine("4. Azure needs containerName — our interface doesn't have it");
Console.WriteLine();
Console.WriteLine("=== Options (all bad without Adapter) ===");
Console.WriteLine("1. Modify IFileRepository → breaks hundreds of services");
Console.WriteLine("2. Modify ThirdPartyAzureBlobClient → we don't own it (NuGet package)");
Console.WriteLine("3. Rewrite DocumentService for Azure → code duplication, violates DIP");
Console.WriteLine("4. Use Adapter Pattern → wrap Azure client behind our interface!");
