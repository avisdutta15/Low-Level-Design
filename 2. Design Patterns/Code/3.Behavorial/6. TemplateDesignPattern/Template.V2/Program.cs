using Template.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT TEMPLATE METHOD PATTERN
// =============================================================================

Console.WriteLine("=== Template Method: Fixed skeleton, varying steps ===");
Console.WriteLine();

var records = new[] { "Alice,report.pdf,2024-01-15", "Bob,data.csv,2024-01-16" };

Console.WriteLine("--- S3 Export (Parquet format, IAM auth) ---");
Console.WriteLine();
BaseDataExporter exporter = new S3DataExporter();
exporter.Export(records);

Console.WriteLine();
Console.WriteLine("--- Azure Export (JSON format, Managed Identity) ---");
Console.WriteLine();
exporter = new AzureBlobDataExporter();
exporter.Export(records);

Console.WriteLine();
Console.WriteLine("--- Local Export (CSV format, no auth, allows empty) ---");
Console.WriteLine();
exporter = new LocalFileDataExporter();
exporter.Export(records);

Console.WriteLine();
Console.WriteLine("--- Empty records: S3 rejects, Local allows ---");
Console.WriteLine();
Console.WriteLine("  S3:");
new S3DataExporter().Export(Array.Empty<string>());
Console.WriteLine();
Console.WriteLine("  Local (allows empty):");
new LocalFileDataExporter().Export(Array.Empty<string>());

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. DRY: Workflow defined ONCE in base class (Connect→Validate→Transform→Write→Disconnect)");
Console.WriteLine("2. CONSISTENCY: All exporters guaranteed to follow the same steps in the same order");
Console.WriteLine("3. SHARED LOGIC: Validation runs once — fix it once, all exporters benefit");
Console.WriteLine("4. ONLY VARY WHAT DIFFERS: Subclasses implement only the parts that change");
Console.WriteLine("5. HOOKS: Optional steps (OnExportComplete) — override only if needed");
Console.WriteLine("6. OVERRIDE SELECTIVELY: Local overrides Validate() to allow empty files");
Console.WriteLine("7. ADDING NEW EXPORTER: Just extend base class, implement abstract steps");
