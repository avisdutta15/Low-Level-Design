namespace Template.V1;

/// <summary>
/// Another exporter — same 5-step structure, different details.
/// Steps 1, 2, 5 are almost identical (connect, validate, disconnect).
/// Only steps 3 and 4 actually differ (transform + write).
/// 90% of this code is duplicated from S3DataExporter!
/// </summary>
public class AzureBlobDataExporter
{
    public void Export(string[] records)
    {
        // Step 1: Connect (different auth, same concept)
        Console.WriteLine("  [Azure] Connecting to Azure Blob Storage...");
        Console.WriteLine("  [Azure] Authenticating with Managed Identity...");

        // Step 2: Validate (IDENTICAL to S3)
        Console.WriteLine("  [Azure] Validating records...");
        if (records.Length == 0)
        {
            Console.WriteLine("  [Azure] ERROR: No records to export");
            return;
        }

        // Step 3: Transform data (JSON instead of Parquet)
        Console.WriteLine("  [Azure] Transforming to JSON format...");
        var transformed = records.Select(r => $"{{\"data\":\"{r}\"}}").ToArray();

        // Step 4: Write (different path format)
        Console.WriteLine($"  [Azure] Writing {transformed.Length} records to container/exports/");
        foreach (var record in transformed)
            Console.WriteLine($"      → {record}");

        // Step 5: Disconnect (IDENTICAL concept)
        Console.WriteLine("  [Azure] Closing connection");
        Console.WriteLine("  [Azure] Export complete");
    }
}
