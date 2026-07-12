namespace Facade.V1;

public class VirusScanService
{
    public bool Scan(byte[] content)
    {
        Console.WriteLine($"  [VirusScan] Scanning {content.Length} bytes...");
        Console.WriteLine($"  [VirusScan] Clean - no threats detected");
        return true; // true = safe
    }
}
