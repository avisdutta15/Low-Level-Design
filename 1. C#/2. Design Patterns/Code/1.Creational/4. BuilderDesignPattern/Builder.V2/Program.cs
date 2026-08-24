using Builder.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT BUILDER PATTERN (Fluent API)
// =============================================================================

Console.WriteLine("=== With Builder: Clean, readable, validated ===");
Console.WriteLine();

// Full-featured S3 config — reads like English
var s3Config = new StorageConfigBuilder("s3", "my-documents-bucket")
    .WithRegion("us-west-2")
    .WithMaxRetries(5)
    .WithTimeout(60)
    .WithEncryption("AES-256-my-secret-key")
    .WithVersioning()
    .WithLogging("/var/logs/storage.log")
    .WithMaxFileSize(104857600) // 100MB
    .WithAllowedExtensions(".pdf", ".docx", ".xlsx")
    .Build();

Console.WriteLine("Full S3 config:");
s3Config.PrintConfig();

Console.WriteLine();
Console.WriteLine("=== Simple local config (only required params + defaults) ===");
Console.WriteLine();

// Minimal config — just provider and bucket, everything else uses defaults
var localConfig = new StorageConfigBuilder("local", "/tmp/files")
    .Build();

Console.WriteLine("Simple local config:");
localConfig.PrintConfig();

Console.WriteLine();
Console.WriteLine("=== Azure with selective options ===");
Console.WriteLine();

// Pick only the options you need — no null parade
var azureConfig = new StorageConfigBuilder("azure", "my-container")
    .WithRegion("westeurope")
    .WithEncryption("AES-256-azure-key")
    .WithMaxFileSize(52428800) // 50MB
    .Build();

Console.WriteLine("Azure config:");
azureConfig.PrintConfig();

Console.WriteLine();
Console.WriteLine("=== Validation: Builder prevents invalid state ===");
Console.WriteLine();

try
{
    // Encryption enabled but no key — Build() catches this!
    var invalid = new StorageConfigBuilder("s3", "bucket")
        .WithEncryption("") // empty key
        .Build();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Caught: {ex.Message}");
}

try
{
    // Logging enabled but no path
    var invalid = new StorageConfigBuilder("s3", "bucket")
        .WithLogging("") // empty path
        .Build();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Caught: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. READABLE: Each parameter is named — no positional guessing");
Console.WriteLine("2. OPTIONAL PARAMS: Only set what you need, defaults handle the rest");
Console.WriteLine("3. VALIDATED: Build() enforces business rules (encryption needs key)");
Console.WriteLine("4. IMMUTABLE: StorageConfig is read-only after construction");
Console.WriteLine("5. FLUENT: Method chaining makes construction expressive");
Console.WriteLine("6. DISCOVERABILITY: IDE autocomplete shows available options");
