using Singleton.V6;

// =============================================================================
// V6 DEMO: Singleton + Cloning
// =============================================================================

Console.WriteLine("=== Safe Singleton: Clone() returns same instance ===");

var instance = AppConfiguration.GetInstance();
instance.Theme = "Dark";

var cloned = (AppConfiguration)instance.Clone();

Console.WriteLine($"Same instance? {ReferenceEquals(instance, cloned)}"); // True
Console.WriteLine($"Clone's Theme: {cloned.Theme}"); // Dark (same object)

Console.WriteLine();
Console.WriteLine("=== Broken Singleton: MemberwiseClone creates a copy ===");

var broken = BrokenSingleton.GetInstance();
broken.Name = "Original";

var copy = broken.UnsafeClone();
copy.Name = "Cloned Copy";

Console.WriteLine($"Same instance? {ReferenceEquals(broken, copy)}"); // False!
Console.WriteLine($"Original name: {broken.Name}"); // Original
Console.WriteLine($"Copy name: {copy.Name}"); // Cloned Copy
Console.WriteLine("→ Two separate objects exist. Singleton contract violated!");

Console.WriteLine();
Console.WriteLine("=== Protection Strategies ===");
Console.WriteLine("1. Don't implement ICloneable (best — no Clone method = no cloning)");
Console.WriteLine("2. Clone() returns GetInstance() (safe — always returns the singleton)");
Console.WriteLine("3. Clone() throws InvalidOperationException (fail-fast on misuse)");
Console.WriteLine("4. sealed prevents subclass from adding a Clone method");
