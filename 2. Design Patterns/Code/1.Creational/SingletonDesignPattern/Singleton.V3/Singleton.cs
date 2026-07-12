namespace Singleton.V3;

// =============================================================================
// V3: THREAD-SAFE SINGLETON WITH DOUBLE-CHECKED LOCKING
// =============================================================================
//
// WHY DO WE NEED DOUBLE-CHECKED LOCKING?
//
// Naive approach (single lock):
//
//   public static AppConfiguration GetInstance()
//   {
//       lock (_lock)                    // ← EVERY call pays lock overhead
//       {
//           if (_instance == null)
//               _instance = new AppConfiguration();
//           return _instance;
//       }
//   }
//
// Problem: After the instance is created, the lock is still acquired on EVERY
// call to GetInstance(). Locks are expensive — they involve kernel transitions,
// memory barriers, and thread scheduling. For a hot path (called thousands of
// times), this kills performance.
//
// DOUBLE-CHECKED LOCKING solves this:
//   - First check (outside lock): Fast path — if instance exists, return immediately.
//     No lock overhead for the 99.9% of calls after initialization.
//   - Lock: Only entered during the brief initialization window.
//   - Second check (inside lock): Ensures only ONE thread creates the instance.
//     Between the first check and acquiring the lock, another thread may have
//     already created it.
//
// WHY IS THE SECOND CHECK NEEDED?
//
//   Thread A: _instance == null? YES → tries to acquire lock
//   Thread B: _instance == null? YES → tries to acquire lock
//   Thread A: acquires lock → creates instance → releases lock
//   Thread B: acquires lock → WITHOUT second check, creates ANOTHER instance!
//   
//   With second check:
//   Thread B: acquires lock → _instance == null? NO → returns existing instance ✓
//
// =============================================================================

public class AppConfiguration
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

    private AppConfiguration()
    {
        Console.WriteLine("  [Constructor] AppConfiguration instance created.");
    }

    public static AppConfiguration GetInstance()
    {
        // First check: no lock, fast return if already initialized
        if (_instance == null)
        {
            // Only threads that see null reach here (brief window during startup)
            lock (_lock)
            {
                // Second check: inside the lock, verify again
                // Another thread may have created it while we waited for the lock
                if (_instance == null)
                {
                    _instance = new AppConfiguration();
                }
            }
        }

        // After initialization, all calls skip both the lock and the inner check
        return _instance;
    }

    // --- Business logic ---
    private readonly Dictionary<string, string> _settings = new();

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            _settings[key] = value;
        }
    }

    public string? Get(string key)
    {
        lock (_lock)
        {
            return _settings.TryGetValue(key, out var val) ? val : null;
        }
    }
}
