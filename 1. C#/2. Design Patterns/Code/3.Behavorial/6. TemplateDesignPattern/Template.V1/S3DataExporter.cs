namespace Template.V1;

/// <summary>
/// Without Template Method: Each exporter duplicates the entire export workflow.
/// The overall steps are the SAME (connect, validate, transform, write, disconnect),
/// but specific details vary per provider. Code is copy-pasted between classes.
/// </summary>
public class S3DataExporter
{
    public void Export(string[] records)
    {
        // Step 1: Connect
        Console.WriteLine("  [S3] Connecting to AWS S3...");
        Console.WriteLine("  [S3] Authenticating with IAM role...");

        // Step 2: Validate
        Console.WriteLine("  [S3] Validating records...");
        if (records.Length == 0)
        {
            Console.WriteLine("  [S3] ERROR: No records to export");
            return;
        }

        // Step 3: Transform data
        Console.WriteLine("  [S3] Transforming to Parquet format...");
        var transformed = records.Select(r => $"PARQUET:{r}").ToArray();

        // Step 4: Write
        Console.WriteLine($"  [S3] Writing {transformed.Length} records to s3://bucket/exports/");
        foreach (var record in transformed)
            Console.WriteLine($"      → {record}");

        // Step 5: Disconnect
        Console.WriteLine("  [S3] Closing connection");
        Console.WriteLine("  [S3] Export complete");
    }
}
