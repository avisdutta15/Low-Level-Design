namespace Template.V2;

/// <summary>
/// Concrete implementation: exports to AWS S3 in Parquet format.
/// Only provides the VARYING parts — skeleton is inherited.
/// </summary>
public class S3DataExporter : BaseDataExporter
{
    protected override void Connect()
    {
        Console.WriteLine("  [S3] Connecting to AWS S3...");
        Console.WriteLine("  [S3] Authenticating with IAM role...");
    }

    protected override string[] Transform(string[] records)
    {
        Console.WriteLine("  [S3] Transforming to Parquet format...");
        return records.Select(r => $"PARQUET:{r}").ToArray();
    }

    protected override void Write(string[] transformedRecords)
    {
        Console.WriteLine($"  [S3] Writing {transformedRecords.Length} records to s3://bucket/exports/");
        foreach (var record in transformedRecords)
            Console.WriteLine($"      → {record}");
    }

    protected override void Disconnect()
    {
        Console.WriteLine("  [S3] Closing S3 connection");
    }

    protected override void OnExportComplete(int recordCount)
    {
        Console.WriteLine($"  [S3] Metrics: {recordCount} records exported to S3");
    }
}
