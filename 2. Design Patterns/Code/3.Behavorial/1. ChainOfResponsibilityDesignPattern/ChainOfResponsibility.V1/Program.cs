using ChainOfResponsibility.V1;

// =============================================================================
// V1: WHY DO WE NEED CHAIN OF RESPONSIBILITY?
// =============================================================================

Console.WriteLine("=== Without Chain of Responsibility ===");
Console.WriteLine();

var service = new FileUploadService();

Console.WriteLine("--- Valid upload (passes all checks) ---");
Console.WriteLine();
service.Upload(new UploadRequest
{
    FileName = "report.pdf",
    Content = new byte[1024],
    Author = "Alice",
    UserRole = "writer"
});

Console.WriteLine();
Console.WriteLine("--- Rejected: wrong role ---");
Console.WriteLine();
service.Upload(new UploadRequest
{
    FileName = "report.pdf",
    Content = new byte[1024],
    Author = "Bob",
    UserRole = "reader"
});

Console.WriteLine();
Console.WriteLine("--- Rejected: file too large ---");
Console.WriteLine();
service.Upload(new UploadRequest
{
    FileName = "bigfile.pdf",
    Content = new byte[20 * 1024 * 1024], // 20MB
    Author = "Alice",
    UserRole = "writer"
});

Console.WriteLine();
Console.WriteLine("--- Rejected: bad extension ---");
Console.WriteLine();
service.Upload(new UploadRequest
{
    FileName = "malware.exe",
    Content = new byte[1024],
    Author = "Alice",
    UserRole = "writer"
});

Console.WriteLine();
Console.WriteLine("=== Problems with this approach ===");
Console.WriteLine("1. SRP VIOLATION: One method handles 5 different validation concerns");
Console.WriteLine("2. OCP VIOLATION: Adding rate-limiting = modifying the Upload method");
Console.WriteLine("3. NOT REUSABLE: Can't reuse 'size check' for a different workflow");
Console.WriteLine("4. NOT CONFIGURABLE: Can't skip virus scan for trusted internal uploads");
Console.WriteLine("5. HARD TO TEST: Must test all 5 validations through one entry point");
Console.WriteLine("6. RIGID ORDERING: Can't reorder checks without restructuring the method");
Console.WriteLine("7. DEEPLY NESTED: More checks = deeper nesting or early returns scattered throughout");
