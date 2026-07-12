using State.V1;

// =============================================================================
// V1: WHY DO WE NEED THE STATE PATTERN?
// =============================================================================

Console.WriteLine("=== Without State Pattern: if/else everywhere ===");
Console.WriteLine();

var job = new FileUploadJob("report.pdf", new byte[1024]);

Console.WriteLine("--- Happy path ---");
Console.WriteLine();
job.Validate();
job.Upload();

Console.WriteLine();
Console.WriteLine("--- Try invalid transitions ---");
Console.WriteLine();
job.Validate();  // already completed
job.Upload();    // already completed
job.Cancel();    // can't cancel completed

Console.WriteLine();
Console.WriteLine("--- Failed job + retry ---");
Console.WriteLine();
var bigJob = new FileUploadJob("huge.zip", new byte[20 * 1024 * 1024]);
bigJob.Validate();       // fails (too large)
bigJob.Upload();         // can't upload failed job
bigJob.Retry();          // reset to pending
bigJob.Validate();       // still too large

Console.WriteLine();
Console.WriteLine("--- Cancel ---");
Console.WriteLine();
var cancelJob = new FileUploadJob("draft.txt", new byte[100]);
cancelJob.Cancel();      // cancel from pending

Console.WriteLine();
Console.WriteLine("=== Problems ===");
Console.WriteLine("1. Every method has 5+ if/else branches for each state");
Console.WriteLine("2. Adding a 'Queued' state = modifying EVERY method");
Console.WriteLine("3. State-specific behavior is scattered across the class");
Console.WriteLine("4. Invalid transitions handled inconsistently");
Console.WriteLine("5. Class grows linearly with states × methods");
Console.WriteLine("6. Hard to see the state machine — transitions buried in conditionals");
