using State.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT STATE PATTERN
// =============================================================================

Console.WriteLine("=== State Pattern: Each state is a class ===");
Console.WriteLine();

Console.WriteLine("--- Happy path: Pending → Validated → Uploading → Completed ---");
Console.WriteLine();
var job = new FileUploadJob("report.pdf", new byte[1024]);
job.Validate();
job.Upload();

Console.WriteLine();
Console.WriteLine("--- Try invalid transitions on completed job ---");
Console.WriteLine();
job.Validate();
job.Upload();
job.Cancel();

Console.WriteLine();
Console.WriteLine("--- Failed job: Pending → Failed → Retry → Pending ---");
Console.WriteLine();
var bigJob = new FileUploadJob("huge.zip", new byte[20 * 1024 * 1024]);
bigJob.Validate();   // fails: too large
bigJob.Upload();     // can't upload from Failed
bigJob.Retry();      // reset to Pending
bigJob.Validate();   // fails again (content still too large)

Console.WriteLine();
Console.WriteLine("--- Cancel from Pending ---");
Console.WriteLine();
var cancelJob = new FileUploadJob("draft.txt", new byte[100]);
cancelJob.Cancel();
cancelJob.Upload();  // can't upload cancelled job

Console.WriteLine();
Console.WriteLine("--- Cancel from Validated ---");
Console.WriteLine();
var cancelJob2 = new FileUploadJob("notes.txt", new byte[200]);
cancelJob2.Validate();
cancelJob2.Cancel();

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. NO IF/ELSE: Each state class handles only its own behavior");
Console.WriteLine("2. OCP: Add a 'Queued' state = new class. No existing code modified");
Console.WriteLine("3. SRP: PendingState only knows about Pending behavior");
Console.WriteLine("4. VISIBLE STATE MACHINE: State classes + transitions = clear picture");
Console.WriteLine("5. TESTABLE: Test each state in isolation");
Console.WriteLine("6. TRANSITIONS: State classes control when/where to transition");
