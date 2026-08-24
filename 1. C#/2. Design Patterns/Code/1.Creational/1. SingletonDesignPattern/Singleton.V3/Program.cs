using Singleton.V3;

// =============================================================================
// V3 DEMO: Thread-safe Singleton with double-checked locking
// =============================================================================

Console.WriteLine("=== Multi-threaded: Double-checked locking ===");

var tasks = new Task[20];
var instances = new AppConfiguration[20];

for (int i = 0; i < 20; i++)
{
    int index = i;
    tasks[i] = Task.Run(() =>
    {
        instances[index] = AppConfiguration.GetInstance();
    });
}

Task.WaitAll(tasks);

// Only ONE constructor call, all threads get the same instance
var allSame = instances.All(inst => ReferenceEquals(inst, instances[0]));
Console.WriteLine($"All 20 threads got same instance? {allSame}"); // Always True

Console.WriteLine();
Console.WriteLine("=== Performance benefit of double-check ===");
Console.WriteLine("After initialization, GetInstance() is just a null check — no lock acquired.");

var sw = System.Diagnostics.Stopwatch.StartNew();
for (int i = 0; i < 1_000_000; i++)
{
    _ = AppConfiguration.GetInstance();
}
sw.Stop();
Console.WriteLine($"1,000,000 calls to GetInstance(): {sw.ElapsedMilliseconds}ms");
Console.WriteLine("(Lock is never entered after first call — near-zero overhead)");
