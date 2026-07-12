// =============================================================================
// V1: WHY DO WE NEED SINGLETON?
// =============================================================================
// 
// Problem: Without Singleton, every time we create a new instance of a class,
// we get a completely separate object with its own state.
//
// Consider a DatabaseConnection class — if every service creates its own
// instance, we end up with:
//   - Multiple connections to the same DB (resource waste)
//   - Inconsistent state across the application
//   - No centralized control over the shared resource
//
// The Singleton pattern ensures:
//   1. Only ONE instance of a class exists throughout the application lifetime
//   2. A global access point to that instance
//   3. Controlled initialization (lazy or eager)
// =============================================================================

// --- WITHOUT SINGLETON: The Problem ---

var config1 = new AppConfiguration();
config1.Set("Theme", "Dark");

var config2 = new AppConfiguration();
config2.Set("Theme", "Light");

Console.WriteLine("=== Without Singleton ===");
Console.WriteLine($"config1 Theme: {config1.Get("Theme")}"); // Dark
Console.WriteLine($"config2 Theme: {config2.Get("Theme")}"); // Light
Console.WriteLine($"Same instance? {ReferenceEquals(config1, config2)}"); // False

// Two different objects — changes to one don't reflect in the other.
// If Module A reads config1 and Module B reads config2, they see different values.
// This is the core problem Singleton solves.

Console.WriteLine();
Console.WriteLine("=== Problem Scenarios Where Singleton is Needed ===");
Console.WriteLine("1. Database Connection Pool — one pool shared across all services");
Console.WriteLine("2. Logger — one logger with consistent file handle");
Console.WriteLine("3. Configuration Manager — one source of truth for app settings");
Console.WriteLine("4. Cache — one shared cache, not duplicated per module");
Console.WriteLine("5. Thread Pool — controlled number of threads, centrally managed");

// --- The class without singleton ---

public class AppConfiguration
{
    private readonly Dictionary<string, string> _settings = new();

    public void Set(string key, string value) => _settings[key] = value;
    public string? Get(string key) => _settings.TryGetValue(key, out var val) ? val : null;
}
