using Strategy.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT STRATEGY PATTERN
// =============================================================================

Console.WriteLine("=== Strategy Pattern: Swap algorithms at runtime ===");
Console.WriteLine();

var data = new byte[10000];

Console.WriteLine("--- Upload with GZip strategy ---");
Console.WriteLine();
var storage = new FileStorageService(new GZipCompressionStrategy());
storage.Upload("report.pdf", data);

Console.WriteLine();
Console.WriteLine("--- Upload with LZ4 strategy (fast, less compression) ---");
Console.WriteLine();
storage.SetCompressionStrategy(new LZ4CompressionStrategy());
storage.Upload("realtime-data.bin", data);

Console.WriteLine();
Console.WriteLine("--- Upload with Zip strategy (best compression) ---");
Console.WriteLine();
storage.SetCompressionStrategy(new ZipCompressionStrategy());
storage.Upload("archive.tar", data);

Console.WriteLine();
Console.WriteLine("--- No compression (for already compressed files) ---");
Console.WriteLine();
storage.SetCompressionStrategy(new NoCompressionStrategy());
storage.Upload("photo.jpg", data); // JPEGs are already compressed

Console.WriteLine();
Console.WriteLine("--- Context-based strategy selection ---");
Console.WriteLine();

// Choose strategy based on file type
string fileName = "logs.txt";
ICompressionStrategy strategy = fileName switch
{
    var f when f.EndsWith(".jpg") || f.EndsWith(".png") => new NoCompressionStrategy(),
    var f when f.EndsWith(".log") || f.EndsWith(".txt") => new GZipCompressionStrategy(),
    var f when f.EndsWith(".bin") => new LZ4CompressionStrategy(),
    _ => new ZipCompressionStrategy()
};

var smartStorage = new FileStorageService(strategy);
smartStorage.Upload(fileName, data);

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. OCP: Add BrotliStrategy = new class. Zero changes to FileStorageService.");
Console.WriteLine("2. SRP: Each strategy class contains ONLY its algorithm logic");
Console.WriteLine("3. RUNTIME SWAP: SetCompressionStrategy() changes behavior without restart");
Console.WriteLine("4. TESTABLE: Test each strategy in isolation, mock strategy for service tests");
Console.WriteLine("5. NO IF/ELSE: Context delegates — polymorphism handles dispatch");
Console.WriteLine("6. COMPOSABLE: Strategy selection can be driven by config, file type, user pref, etc.");
