namespace Template.V2;

/// <summary>
/// Concrete implementation: exports to local file system in CSV format.
/// </summary>
public class LocalFileDataExporter : BaseDataExporter
{
    protected override void Connect()
    {
        Console.WriteLine("  [Local] Ensuring output directory exists...");
    }

    protected override string[] Transform(string[] records)
    {
        Console.WriteLine("  [Local] Transforming to CSV format...");
        return records.Select(r => $"\"{r}\"").ToArray();
    }

    protected override void Write(string[] transformedRecords)
    {
        Console.WriteLine($"  [Local] Writing {transformedRecords.Length} records to /exports/data.csv");
        foreach (var record in transformedRecords)
            Console.WriteLine($"      → {record}");
    }

    protected override void Disconnect()
    {
        Console.WriteLine("  [Local] Closing file handle");
    }

    // Override validation: local export allows empty records (creates empty file)
    protected override bool Validate(string[] records)
    {
        Console.WriteLine($"  [Local] Validating (local allows empty files)...");
        return true; // Always passes for local
    }
}
