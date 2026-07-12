namespace Singleton.V4;

// =============================================================================
// V4: SEALED SINGLETON
// =============================================================================
//
// WHY DOES THE SINGLETON NEED TO BE SEALED?
//
// Problem without sealed:
//
//   public class AppConfiguration { ... private AppConfiguration() { } ... }
//
//   public class EvilConfig : AppConfiguration  // ← Compiles if not sealed!
//   {
//       // A derived class can:
//       // 1. Create additional instances (bypassing the private constructor via
//       //    nested class tricks or reflection)
//       // 2. Override virtual methods, changing singleton behavior
//       // 3. Be instantiated multiple times — violating the "one instance" contract
//   }
//
// Wait — a private constructor prevents inheritance, right?
// 
// MOSTLY, but not completely:
//   - A NESTED class inside the singleton CAN access the private constructor
//   - Reflection can bypass access modifiers entirely
//   - In some languages/frameworks, serialization can create instances
//
// By marking the class `sealed`:
//   1. The compiler GUARANTEES no class can inherit from it
//   2. It communicates INTENT — "this class is complete, don't extend it"
//   3. Minor performance benefit — the JIT can devirtualize method calls
//   4. Defense in depth — even if someone finds a way around the private
//      constructor, they can't subclass and create a "second singleton"
//
// It's a belt-and-suspenders approach: private constructor + sealed = airtight.
// =============================================================================

public sealed class AppConfiguration
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

    // Private constructor — no external instantiation
    private AppConfiguration()
    {
        Console.WriteLine("  [Constructor] AppConfiguration instance created.");
    }

    public static AppConfiguration GetInstance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new AppConfiguration();
                }
            }
        }
        return _instance;
    }

    // --- Business logic ---
    private readonly Dictionary<string, string> _settings = new();

    public void Set(string key, string value)
    {
        lock (_lock) { _settings[key] = value; }
    }

    public string? Get(string key)
    {
        lock (_lock) { return _settings.TryGetValue(key, out var val) ? val : null; }
    }
}

// =============================================================================
// DEMONSTRATION: What happens without sealed
// =============================================================================

// This would compile if AppConfiguration were NOT sealed:
//
// public class MaliciousConfig : AppConfiguration
// {
//     // Even though the base constructor is private, a nested class hack
//     // or reflection could bypass it. With 'sealed', this line won't compile:
//     // error CS0509: 'MaliciousConfig': cannot derive from sealed type 'AppConfiguration'
// }
