using System.Reflection;
using Singleton.V7;

// =============================================================================
// V7 DEMO: Singleton + Reflection Attack
// =============================================================================

Console.WriteLine("=== Normal usage ===");
var instance = AppConfiguration.GetInstance();
instance.Theme = "Dark";
Console.WriteLine($"Instance theme: {instance.Theme}");

Console.WriteLine();
Console.WriteLine("=== Reflection attack: Trying to create a second instance ===");

try
{
    // Get the private constructor
    var constructor = typeof(AppConfiguration).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        Type.EmptyTypes,
        null);

    Console.WriteLine($"Found private constructor: {constructor != null}");

    // Try to invoke it — this should throw!
    var reflectedInstance = (AppConfiguration)constructor!.Invoke(null);

    // If we reach here, the guard failed
    Console.WriteLine("DANGER: Second instance created! Singleton broken!");
}
catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
{
    Console.WriteLine($"BLOCKED: {ex.InnerException.Message}");
    Console.WriteLine("→ Constructor guard prevented reflection attack ✓");
}

Console.WriteLine();
Console.WriteLine("=== Lazy<T> singleton — also reflection-safe ===");

var lazy1 = LazyConfiguration.Instance;
lazy1.Mode = "Production";

var lazy2 = LazyConfiguration.Instance;
Console.WriteLine($"Same instance? {ReferenceEquals(lazy1, lazy2)}"); // True
Console.WriteLine($"Mode: {lazy2.Mode}");

Console.WriteLine();
Console.WriteLine("=== Summary of all defenses ===");
Console.WriteLine("V2: Private constructor → prevents new T()");
Console.WriteLine("V3: Double-checked lock → prevents race condition");
Console.WriteLine("V4: Sealed → prevents subclass creating instances");
Console.WriteLine("V5: Custom deserializer → prevents serialization bypass");
Console.WriteLine("V6: Clone() returns this → prevents cloning bypass");
Console.WriteLine("V7: Constructor guard → prevents reflection bypass");
Console.WriteLine();
Console.WriteLine("All combined = bulletproof Singleton.");
