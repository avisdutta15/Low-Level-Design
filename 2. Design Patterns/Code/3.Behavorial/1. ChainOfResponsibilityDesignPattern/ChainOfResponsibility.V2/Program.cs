using ChainOfResponsibility.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT CHAIN OF RESPONSIBILITY
// =============================================================================

Console.WriteLine("=== Chain of Responsibility: Composable validation pipeline ===");
Console.WriteLine();

// Build the chain: Auth → Size → Extension → VirusScan → Duplicate
var auth = new AuthenticationHandler();
var size = new FileSizeHandler();
var extension = new ExtensionHandler();
var virusScan = new VirusScanHandler();
var duplicate = new DuplicateCheckHandler(new[] { "existing.pdf" });

auth.SetNext(size).SetNext(extension).SetNext(virusScan).SetNext(duplicate);

// The chain starts at auth — request flows through each handler
IUploadHandler pipeline = auth;

Console.WriteLine("--- Valid upload (passes all handlers) ---");
Console.WriteLine();
bool result = pipeline.Handle(new UploadRequest
{
    FileName = "report.pdf",
    Content = new byte[1024],
    Author = "Alice",
    UserRole = "writer"
});
Console.WriteLine($"  Result: {(result ? "UPLOADED" : "REJECTED")}");

Console.WriteLine();
Console.WriteLine("--- Rejected at Auth handler (reader can't upload) ---");
Console.WriteLine();
result = pipeline.Handle(new UploadRequest
{
    FileName = "report.pdf",
    Content = new byte[1024],
    UserRole = "reader"
});
Console.WriteLine($"  Result: {(result ? "UPLOADED" : "REJECTED")}");

Console.WriteLine();
Console.WriteLine("--- Rejected at Size handler (too large) ---");
Console.WriteLine();
result = pipeline.Handle(new UploadRequest
{
    FileName = "bigfile.pdf",
    Content = new byte[20 * 1024 * 1024],
    UserRole = "admin"
});
Console.WriteLine($"  Result: {(result ? "UPLOADED" : "REJECTED")}");

Console.WriteLine();
Console.WriteLine("--- Rejected at Extension handler (.exe blocked) ---");
Console.WriteLine();
result = pipeline.Handle(new UploadRequest
{
    FileName = "malware.exe",
    Content = new byte[100],
    UserRole = "writer"
});
Console.WriteLine($"  Result: {(result ? "UPLOADED" : "REJECTED")}");

Console.WriteLine();
Console.WriteLine("--- Rejected at Duplicate handler (file exists) ---");
Console.WriteLine();
result = pipeline.Handle(new UploadRequest
{
    FileName = "existing.pdf",
    Content = new byte[100],
    UserRole = "writer"
});
Console.WriteLine($"  Result: {(result ? "UPLOADED" : "REJECTED")}");

Console.WriteLine();
Console.WriteLine("=== Different chain for trusted internal uploads (skip virus scan) ===");
Console.WriteLine();

// Build a shorter chain: Auth → Size → Extension (no virus scan, no duplicate check)
var internalAuth = new AuthenticationHandler();
var internalSize = new FileSizeHandler();
var internalExtension = new ExtensionHandler();

internalAuth.SetNext(internalSize).SetNext(internalExtension);

IUploadHandler internalPipeline = internalAuth;

result = internalPipeline.Handle(new UploadRequest
{
    FileName = "internal-report.pdf",
    Content = new byte[2048],
    UserRole = "admin"
});
Console.WriteLine($"  Result: {(result ? "UPLOADED" : "REJECTED")}");
Console.WriteLine("  (No virus scan or duplicate check — trusted internal upload)");

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. SINGLE RESPONSIBILITY: Each handler does ONE validation");
Console.WriteLine("2. OPEN/CLOSED: Add new handler without modifying existing ones");
Console.WriteLine("3. CONFIGURABLE: Build different chains for different scenarios");
Console.WriteLine("4. REUSABLE: Same FileSizeHandler works in upload, download, or any pipeline");
Console.WriteLine("5. TESTABLE: Test each handler in isolation");
Console.WriteLine("6. ORDERABLE: Reorder handlers by changing chain construction");
