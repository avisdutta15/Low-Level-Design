using Template.V1;

// =============================================================================
// V1: WHY DO WE NEED THE TEMPLATE METHOD PATTERN?
// =============================================================================

Console.WriteLine("=== Without Template Method: Duplicated structure everywhere ===");
Console.WriteLine();

var records = new[] { "Alice,report.pdf,2024-01-15", "Bob,data.csv,2024-01-16" };

Console.WriteLine("--- S3 Export ---");
Console.WriteLine();
new S3DataExporter().Export(records);

Console.WriteLine();
Console.WriteLine("--- Azure Export ---");
Console.WriteLine();
new AzureBlobDataExporter().Export(records);

Console.WriteLine();
Console.WriteLine("--- Local File Export ---");
Console.WriteLine();
new LocalFileDataExporter().Export(records);

Console.WriteLine();
Console.WriteLine("=== Problems ===");
Console.WriteLine("1. DUPLICATED STRUCTURE: All 3 classes follow Connect→Validate→Transform→Write→Disconnect");
Console.WriteLine("2. COPY-PASTE BUGS: Validation logic is identical but repeated 3 times");
Console.WriteLine("3. FIX IN ONE, FORGET OTHERS: Bug fix in S3 validate doesn't propagate to Azure/Local");
Console.WriteLine("4. OCP VIOLATION: Changing the export workflow (add logging step) = modify ALL classes");
Console.WriteLine("5. DRY VIOLATION: 70% of code is identical across all 3 exporters");
Console.WriteLine("6. INCONSISTENCY: One exporter might skip validation, another handles it differently");
Console.WriteLine("7. HARD TO ENFORCE: No way to guarantee all exporters follow the same steps");
