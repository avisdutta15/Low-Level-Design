namespace ChainOfResponsibility.V2;

/// <summary>
/// Handler 3: Checks if the file extension is in the allowed list.
/// </summary>
public class ExtensionHandler : BaseUploadHandler
{
    public override bool Handle(UploadRequest request)
    {
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();

        if (!request.AllowedExtensions.Contains(extension))
        {
            Console.WriteLine($"  [Extension] REJECTED: '{extension}' not in allowed list [{string.Join(", ", request.AllowedExtensions)}]");
            return false;
        }

        Console.WriteLine($"  [Extension] PASSED: '{extension}' is allowed");
        return base.Handle(request);
    }
}
