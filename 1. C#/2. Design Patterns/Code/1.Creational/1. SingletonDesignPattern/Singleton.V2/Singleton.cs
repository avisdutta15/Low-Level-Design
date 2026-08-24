namespace Singleton.V2;

// =============================================================================
// V2: BASIC SINGLETON (NOT THREAD-SAFE)
// =============================================================================
// 
// This is the simplest Singleton implementation.
// It works correctly in a single-threaded environment but BREAKS under concurrency.
//
// Why it's NOT thread-safe:
//   Thread A checks _instance == null → true → enters the if block
//   Thread B checks _instance == null → true → enters the if block (before A finishes)
//   Both threads create a new instance → TWO instances exist → Singleton violated!
// =============================================================================

public class AppConfiguration
{
    private static AppConfiguration? _instance;

    // Private constructor — prevents external instantiation
    private AppConfiguration()
    {
        Console.WriteLine("  [Constructor] AppConfiguration instance created.");
    }

    // Public access point
    // Not it is static as we will cannot create any instance ourself.
    public static AppConfiguration GetInstance()
    {
        // NOT ATOMIC: This check + creation is a race condition
        if (_instance == null)
        {
            _instance = new AppConfiguration();
        }
        return _instance;
    }

    // --- Business logic ---
    private readonly Dictionary<string, string> _settings = new();

    public void Set(string key, string value) => _settings[key] = value;
    public string? Get(string key) => _settings.TryGetValue(key, out var val) ? val : null;
}
