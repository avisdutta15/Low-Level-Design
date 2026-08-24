namespace ChainOfResponsibility.V1;

/// <summary>
/// Without Chain of Responsibility: ALL validation logic crammed into one method.
/// 
/// Problems:
///   - Violates SRP: one method handles auth, size check, extension check, virus scan, duplicate check
///   - Adding a new validation = modifying this method (violates OCP)
///   - Can't reorder, skip, or add validations without touching this code
///   - Hard to test individual validations in isolation
///   - Nested if/else becomes deeply indented and unreadable
/// </summary>
public class FileUploadService
{
    public bool Upload(UploadRequest request)
    {
        // Step 1: Authentication check
        if (request.UserRole != "writer" && request.UserRole != "admin")
        {
            Console.WriteLine($"  [REJECTED] User role '{request.UserRole}' cannot upload files");
            return false;
        }
        Console.WriteLine($"  [Auth] User role '{request.UserRole}' authorized to upload");

        // Step 2: File size check
        if (request.Content.Length > request.MaxAllowedSizeBytes)
        {
            Console.WriteLine($"  [REJECTED] File too large: {request.Content.Length} bytes (max: {request.MaxAllowedSizeBytes})");
            return false;
        }
        Console.WriteLine($"  [Size] File size {request.Content.Length} bytes is within limit");

        // Step 3: File extension check
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!request.AllowedExtensions.Contains(extension))
        {
            Console.WriteLine($"  [REJECTED] Extension '{extension}' not allowed. Allowed: {string.Join(", ", request.AllowedExtensions)}");
            return false;
        }
        Console.WriteLine($"  [Extension] Extension '{extension}' is allowed");

        // Step 4: Virus scan
        bool isSafe = SimulateVirusScan(request.Content);
        if (!isSafe)
        {
            Console.WriteLine($"  [REJECTED] File failed virus scan!");
            return false;
        }
        Console.WriteLine($"  [VirusScan] File is clean");

        // Step 5: Duplicate check
        bool isDuplicate = SimulateDuplicateCheck(request.FileName);
        if (isDuplicate)
        {
            Console.WriteLine($"  [REJECTED] File '{request.FileName}' already exists");
            return false;
        }
        Console.WriteLine($"  [Duplicate] No duplicate found");

        // All validations passed — proceed with upload
        Console.WriteLine($"  [Upload] Uploading '{request.FileName}' to storage...");
        return true;
    }

    private bool SimulateVirusScan(byte[] content) => true;
    private bool SimulateDuplicateCheck(string fileName) => false;
}
