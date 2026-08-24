using Decorator.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT DECORATOR PATTERN
// =============================================================================

Console.WriteLine("=== Plain S3 (no decorators) ===");
Console.WriteLine();

IFileRepository repo = new S3FileRepository();
repo.Upload("report.pdf", new byte[] { 1, 2, 3 });
repo.Download("report.pdf");

Console.WriteLine();
Console.WriteLine("=== S3 + Logging (one decorator) ===");
Console.WriteLine();

IFileRepository loggingRepo = new LoggingDecorator(new S3FileRepository());
loggingRepo.Upload("report.pdf", new byte[] { 1, 2, 3 });

Console.WriteLine();
Console.WriteLine("=== S3 + Logging + Caching (stacked decorators) ===");
Console.WriteLine();

IFileRepository cachedLoggingRepo =
    new LoggingDecorator(           // outermost: logs everything
        new CachingDecorator(       // middle: caches results
            new S3FileRepository()  // innermost: actual storage
        )
    );

cachedLoggingRepo.Upload("data.csv", new byte[] { 10, 20, 30 });
Console.WriteLine();
cachedLoggingRepo.Download("data.csv"); // cache miss first time
Console.WriteLine();
cachedLoggingRepo.Download("data.csv"); // cache hit!

Console.WriteLine();
Console.WriteLine("=== S3 + Encryption + Caching + Logging + Retry (all stacked) ===");
Console.WriteLine();

IFileRepository fullStack =
    new LoggingDecorator(                   // 4. Log all operations
        new RetryDecorator(                 // 3. Retry on failure
            new CachingDecorator(           // 2. Cache results
                new EncryptionDecorator(    // 1. Encrypt before storing
                    new S3FileRepository()  // 0. Actual storage
                )
            ),
            maxRetries: 3
        )
    );

fullStack.Upload("secret.pdf", new byte[] { 42, 43, 44 });
Console.WriteLine();
fullStack.Download("secret.pdf");

Console.WriteLine();
Console.WriteLine("=== Same decorators, DIFFERENT provider (Local) ===");
Console.WriteLine();

// Same behaviors, just swap the inner component!
IFileRepository localWithExtras =
    new LoggingDecorator(
        new CachingDecorator(
            new LocalFileRepository()  // <-- different provider, same decorators
        )
    );

localWithExtras.Upload("draft.txt", new byte[] { 7, 8, 9 });
Console.WriteLine();
localWithExtras.Download("draft.txt");

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. COMPOSABLE: Mix any combination of behaviors at runtime");
Console.WriteLine("2. SINGLE RESPONSIBILITY: Each decorator does ONE thing");
Console.WriteLine("3. OPEN/CLOSED: Add new behaviors without modifying existing code");
Console.WriteLine("4. PROVIDER-AGNOSTIC: Same decorators work with S3, Local, Azure, etc.");
Console.WriteLine("5. NO CLASS EXPLOSION: N decorators + M providers = N + M classes (not N*M)");
Console.WriteLine("6. RUNTIME FLEXIBILITY: Choose decorators based on config/environment");
