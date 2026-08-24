namespace Template.V1;

/// <summary>
/// Yet another exporter — same structure, same duplication.
/// </summary>
public class LocalFileDataExporter
{
    public void Export(string[] records)
    {
        // Step 1: Connect (just ensure directory exists)
        Console.WriteLine("  [Local] Ensuring output directory exists...");

        // Step 2: Validate (IDENTICAL again)
        Console.WriteLine("  [Local] Validating records...");
        if (records.Length == 0)
        {
            Console.WriteLine("  [Local] ERROR: No records to export");
            return;
        }

        // Step 3: Transform (CSV format)
        Console.WriteLine("  [Local] Transforming to CSV format...");
        var transformed = records.Select(r => $"\"{r}\"").ToArray();

        // Step 4: Write
        Console.WriteLine($"  [Local] Writing {transformed.Length} records to /exports/data.csv");
        foreach (var record in transformed)
            Console.WriteLine($"      → {record}");

        // Step 5: Disconnect (close file handle)
        Console.WriteLine("  [Local] Closing file handle");
        Console.WriteLine("  [Local] Export complete");
    }
}
