namespace Singleton.V6;

// =============================================================================
// V6: SINGLETON + CLONING
// =============================================================================
//
// PROBLEM:
// If a Singleton implements ICloneable (or has a Clone/MemberwiseClone method),
// calling Clone() creates a NEW instance — violating the "one instance" rule.
//
//   var original = AppConfiguration.GetInstance();
//   var clone = (AppConfiguration)original.Clone();  // ← NEW object!
//   ReferenceEquals(original, clone) → FALSE → Singleton broken!
//
// This can happen when:
//   1. Someone implements ICloneable on the singleton (bad practice)
//   2. MemberwiseClone() is called via reflection (it's a protected method on Object)
//   3. A copy constructor exists
//
// SOLUTIONS:
//
// Option 1: Don't implement ICloneable — simplest and best.
//           If there's no Clone method, no one can clone it.
//
// Option 2: If you MUST implement ICloneable (e.g., interface requirement),
//           make Clone() return the same instance (return this).
//
// Option 3: Throw an exception from Clone() to make the violation obvious.
//
// =============================================================================

// --- Option 2: Clone returns the same instance ---

public sealed class AppConfiguration : ICloneable
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

    public string Theme { get; set; } = "Default";
    public int MaxConnections { get; set; } = 10;

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

    // SAFE: Returns the same singleton instance — no new object created
    public object Clone()
    {
        // Option 2: Return the existing instance
        return GetInstance();

        // Option 3 (alternative): Throw to make misuse obvious
        // throw new InvalidOperationException(
        //     "Singleton cannot be cloned. Use GetInstance() instead.");
    }
}

// --- Demonstrating what would go WRONG without protection ---

public class BrokenSingleton
{
    private static BrokenSingleton? _instance;

    private BrokenSingleton() { }

    public static BrokenSingleton GetInstance()
    {
        return _instance ??= new BrokenSingleton();
    }

    public string Name { get; set; } = "Original";

    // DANGEROUS: This creates a real copy — singleton violated!
    public BrokenSingleton UnsafeClone()
    {
        return (BrokenSingleton)MemberwiseClone();
    }
}
