using Strategy.V1;

// =============================================================================
// V1: WHY DO WE NEED THE STRATEGY PATTERN?
// =============================================================================

Console.WriteLine("=== Without Strategy: if/else for every algorithm ===");
Console.WriteLine();

var data = new byte[10000];

Console.WriteLine("--- GZip ---");
var gzip = new FileCompressor("gzip");
gzip.Compress(data);

Console.WriteLine();
Console.WriteLine("--- Zip ---");
var zip = new FileCompressor("zip");
zip.Compress(data);

Console.WriteLine();
Console.WriteLine("--- LZ4 ---");
var lz4 = new FileCompressor("lz4");
lz4.Compress(data);

Console.WriteLine();
Console.WriteLine("--- Invalid algorithm ---");
try
{
    var invalid = new FileCompressor("brotli");
    invalid.Compress(data);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"  ERROR: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=== Problems ===");
Console.WriteLine("1. OCP VIOLATION: Adding Brotli = modifying Compress() AND Decompress()");
Console.WriteLine("2. SRP VIOLATION: One class contains ALL compression algorithms");
Console.WriteLine("3. IF/ELSE GROWTH: More algorithms = more branches in every method");
Console.WriteLine("4. NOT TESTABLE: Can't test GZip logic without the whole class");
Console.WriteLine("5. CAN'T SWAP AT RUNTIME: Algorithm fixed at construction");
Console.WriteLine("6. CODE DUPLICATION: Same if/else repeated in Compress() and Decompress()");
Console.WriteLine("7. MAGIC STRINGS: 'gzip', 'zip', 'lz4' — no compile-time safety");
