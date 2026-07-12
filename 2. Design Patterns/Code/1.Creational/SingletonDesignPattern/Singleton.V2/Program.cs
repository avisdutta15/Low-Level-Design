using Singleton.V2;

// =============================================================================
// V2 DEMO: Basic Singleton works in single-threaded context
// =============================================================================

Console.WriteLine("=== Single-threaded: Works fine ===");
var instance1 = AppConfiguration.GetInstance();
var instance2 = AppConfiguration.GetInstance();
Console.WriteLine($"Same instance? {ReferenceEquals(instance1, instance2)}"); // True

instance1.Set("Theme", "Dark");
Console.WriteLine($"instance2 sees Theme: {instance2.Get("Theme")}"); // Dark ✓

// =============================================================================
// But under multi-threading, it breaks:
// =============================================================================

Console.WriteLine();
Console.WriteLine("=== Multi-threaded: Race condition demo ===");
Console.WriteLine("(Run multiple times — sometimes you'll see multiple constructor calls)");

// Reset for demo (in real code you can't do this — illustrative only)
var field = typeof(AppConfiguration).GetField("_instance",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
field?.SetValue(null, null);

var tasks = new Task[10];
var instances = new AppConfiguration[10];

for (int i = 0; i < 10; i++)
{
    int index = i;
    tasks[i] = Task.Run(() =>
    {
        instances[index] = AppConfiguration.GetInstance();
    });
}

Task.WaitAll(tasks);

// Check if all threads got the same instance
var allSame = instances.All(inst => ReferenceEquals(inst, instances[0]));
Console.WriteLine($"All 10 threads got same instance? {allSame}");
// Under race conditions, this may print False!
