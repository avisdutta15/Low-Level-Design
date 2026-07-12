namespace ChainOfResponsibility.V2;

/// <summary>
/// Handler 2: Checks if the file size is within the allowed limit.
/// </summary>
public class FileSizeHandler : BaseUploadHandler
{
    public override bool Handle(UploadRequest request)
    {
        if (request.Content.Length > request.MaxAllowedSizeBytes)
        {
            Console.WriteLine($"  [Size] REJECTED: {request.Content.Length} bytes exceeds max {request.MaxAllowedSizeBytes}");
            return false;
        }

        Console.WriteLine($"  [Size] PASSED: {request.Content.Length} bytes within limit");
        return base.Handle(request);
    }
}
