using Builder.V1;

// =============================================================================
// V1: WHY DO WE NEED THE BUILDER PATTERN?
// =============================================================================

Console.WriteLine("=== Without Builder: The Problem ===");
Console.WriteLine();

// What does each parameter mean? You have to check the constructor signature!
var config = new StorageConfig(
    "s3",                          // provider
    "my-documents-bucket",         // bucketName
    "us-east-1",                   // region
    3,                             // maxRetries - or is it timeout?
    30,                            // timeoutSeconds - or is it retries?
    true,                          // enableEncryption - or versioning?
    "AES-256-key-here",            // encryptionKey - or logPath?
    true,                          // enableVersioning
    true,                          // enableLogging
    "/var/logs/storage.log",       // logPath
    104857600,                     // maxFileSizeBytes (100MB)
    new[] { ".pdf", ".docx" }      // allowedExtensions
);

Console.WriteLine("Config created (but look how unreadable the constructor call is!):");
config.PrintConfig();

Console.WriteLine();
Console.WriteLine("=== Problems with this approach ===");
Console.WriteLine("1. TELESCOPING CONSTRUCTOR: 12 parameters, impossible to read");
Console.WriteLine("2. POSITIONAL AMBIGUITY: What does 'true' at position 6 mean?");
Console.WriteLine("3. NO OPTIONAL PARAMS: Must provide ALL values even for defaults");
Console.WriteLine("4. INVALID STATE: Can set enableEncryption=true with encryptionKey=null");
Console.WriteLine("5. NO VALIDATION: Constructor blindly accepts any combination");
Console.WriteLine("6. CONSTRUCTOR OVERLOADS: Need 2^n overloads for n optional params");

Console.WriteLine();
Console.WriteLine("=== The 'null parade' for simple local storage ===");

// I just want local storage with defaults but must provide EVERYTHING
var simpleConfig = new StorageConfig(
    "local",
    "/tmp/files",
    "",              // no region for local
    3,              // default retries
    30,             // default timeout
    false,          // no encryption
    null,           // no key - forced to pass null
    false,          // no versioning
    false,          // no logging
    null,           // no log path - another null
    long.MaxValue,  // no size limit
    Array.Empty<string>() // no extension filter
);

Console.WriteLine("Simple config (all the noise just for basic local storage):");
simpleConfig.PrintConfig();
