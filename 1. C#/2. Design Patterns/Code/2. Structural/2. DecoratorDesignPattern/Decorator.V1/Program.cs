using Decorator.V1;

// =============================================================================
// V1: WHY DO WE NEED THE DECORATOR PATTERN?
// =============================================================================
//
// Scenario: We have a working S3FileRepository. Now we need to add
// cross-cutting concerns: logging, caching, encryption, retry, metrics.
//
// Without Decorator, we must create subclasses for every combination:
//   - S3FileRepositoryWithLogging
//   - S3FileRepositoryWithCaching
//   - S3FileRepositoryWithLoggingAndCaching
//   - S3FileRepositoryWithLoggingAndCachingAndEncryption
//   - ... CLASS EXPLOSION!
// =============================================================================

Console.WriteLine("=== Plain S3 (no extras) ===");
Console.WriteLine();

IFileRepository repo = new S3FileRepository();
repo.Upload("report.pdf", new byte[] { 1, 2, 3 });
repo.Download("report.pdf");

Console.WriteLine();
Console.WriteLine("=== S3 + Logging (separate subclass) ===");
Console.WriteLine();

IFileRepository loggingRepo = new S3FileRepositoryWithLogging();
loggingRepo.Upload("report.pdf", new byte[] { 1, 2, 3 });
loggingRepo.Download("report.pdf");

Console.WriteLine();
Console.WriteLine("=== S3 + Logging + Caching (yet another subclass) ===");
Console.WriteLine();

IFileRepository loggingCachingRepo = new S3FileRepositoryWithLoggingAndCaching();
loggingCachingRepo.Upload("report.pdf", new byte[] { 1, 2, 3 });
loggingCachingRepo.Download("report.pdf");
loggingCachingRepo.Download("report.pdf"); // should hit cache

Console.WriteLine();
Console.WriteLine("=== Problems with this approach ===");
Console.WriteLine("1. CLASS EXPLOSION: For N behaviors x M providers = N*M classes");
Console.WriteLine("   Logging, Caching, Encryption, Retry, Metrics = 5 behaviors");
Console.WriteLine("   S3, Local, Azure = 3 providers");
Console.WriteLine("   Combinations: 2^5 * 3 = 96 classes!");
Console.WriteLine("2. VIOLATES SRP: Each class mixes storage logic + cross-cutting concerns");
Console.WriteLine("3. NOT COMPOSABLE: Can't mix-and-match behaviors at runtime");
Console.WriteLine("4. DUPLICATED LOGIC: Logging code repeated in every subclass");
Console.WriteLine("5. RIGID: Adding a new behavior means new classes for EVERY combination");
Console.WriteLine("6. VIOLATES OCP: Must modify existing class hierarchies to add features");
