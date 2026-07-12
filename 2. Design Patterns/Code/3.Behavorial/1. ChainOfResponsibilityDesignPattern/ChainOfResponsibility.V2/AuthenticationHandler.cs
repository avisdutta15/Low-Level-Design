namespace ChainOfResponsibility.V2;

/// <summary>
/// Handler 1: Checks if the user has permission to upload.
/// </summary>
public class AuthenticationHandler : BaseUploadHandler
{
    public override bool Handle(UploadRequest request)
    {
        if (request.UserRole != "writer" && request.UserRole != "admin")
        {
            Console.WriteLine($"  [Auth] REJECTED: Role '{request.UserRole}' cannot upload");
            return false; // Stop the chain
        }

        Console.WriteLine($"  [Auth] PASSED: Role '{request.UserRole}' authorized");
        return base.Handle(request); // Pass to next handler
    }
}
