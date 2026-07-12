namespace Template.V2;

/// <summary>
/// Concrete implementation: exports to Azure Blob in JSON format.
/// </summary>
public class AzureBlobDataExporter : BaseDataExporter
{
    protected override void Connect()
    {
        Console.WriteLine("  [Azure] Connecting to Azure Blob Storage...");
        Console.WriteLine("  [Azure] Authenticating with Managed Identity...");
    }

    protected override string[] Transform(string[] records)
    {
        Console.WriteLine("  [Azure] Transforming to JSON format...");
        return records.Select(r => $"{{\"data\":\"{r}\"}}").ToArray();
    }

    protected override void Write(string[] transformedRecords)
    {
        Console.WriteLine($"  [Azure] Writing {transformedRecords.Length} records to container/exports/");
        foreach (var record in transformedRecords)
            Console.WriteLine($"      → {record}");
    }

    protected override void Disconnect()
    {
        Console.WriteLine("  [Azure] Closing Azure connection");
    }
}
