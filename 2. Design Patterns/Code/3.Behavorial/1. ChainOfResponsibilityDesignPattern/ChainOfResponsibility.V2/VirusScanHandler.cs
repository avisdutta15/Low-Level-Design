namespace ChainOfResponsibility.V2;

/// <summary>
/// Handler 4: Scans the file content for malware.
/// </summary>
public class VirusScanHandler : BaseUploadHandler
{
    public override bool Handle(UploadRequest request)
    {
        bool isSafe = ScanForViruses(request.Content);

        if (!isSafe)
        {
            Console.WriteLine($"  [VirusScan] REJECTED: Malware detected in '{request.FileName}'!");
            return false;
        }

        Console.WriteLine($"  [VirusScan] PASSED: File is clean");
        return base.Handle(request);
    }

    private bool ScanForViruses(byte[] content)
    {
        // Simulate virus scanning
        return true;
    }
}
