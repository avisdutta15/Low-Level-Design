namespace Singleton.V7;

// =============================================================================
// V7: SINGLETON + REFLECTION
// =============================================================================
//
// PROBLEM:
// Reflection can bypass the private constructor and create new instances.
//
//   var ctor = typeof(AppConfiguration)
//       .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
//   var newInstance = (AppConfiguration)ctor.Invoke(null);
//   // newInstance != GetInstance() → Singleton broken!
//
// This is the NUCLEAR option for breaking singletons. No matter how carefully
// you design the class — private constructor, sealed, no Clone — reflection
// can still create a new instance by directly invoking the constructor.
//
// SOLUTIONS:
//
// 1. Guard in the constructor: If an instance already exists, throw.
//    This makes the second constructor call fail immediately.
//
// 2. Use an enum-based singleton (not idiomatic in C#, common in Java).
//
// 3. Accept it as a limitation: In most real applications, if someone is
//    using reflection to break your singleton, they're either writing tests
//    (legitimate) or doing something adversarial (code review issue).
//
// =============================================================================

public sealed class AppConfiguration
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

    public string Theme { get; set; } = "Default";

    private AppConfiguration()
    {
        // GUARD: If someone tries to create a second instance via reflection, fail!
        if (_instance != null)
        {
            throw new InvalidOperationException(
                "Singleton violation! Use AppConfiguration.GetInstance() instead. " +
                "Cannot create a second instance via reflection.");
        }

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
}

// =============================================================================
// Alternative: Lazy<T> based singleton (simplest thread-safe + reflection-guard)
// =============================================================================

public sealed class LazyConfiguration
{
    private static bool _instantiated;

    private static readonly Lazy<LazyConfiguration> _lazy = new Lazy<Singleton>(() =>
    {
        _instantiated = true;
        return new LazyConfiguration();
    });

    public static LazyConfiguration Instance => _lazy.Value;

    public string Mode { get; set; } = "Default";

    private LazyConfiguration()
    {
        if (_instantiated == true)
        {
            throw new InvalidOperationException(
                "Singleton violation! Use LazyConfiguration.Instance instead.");
        }

        Console.WriteLine("  [Constructor] LazyConfiguration instance created.");
    }
}
