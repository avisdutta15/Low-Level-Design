# Singleton Design Pattern

## Table of Contents

- [What is the Singleton Pattern?](#what-is-the-singleton-pattern)
- [Singleton vs Static Class](#singleton-vs-static-class)
- [V1 — Why Do We Need Singleton?](#v1--why-do-we-need-singleton)
- [V2 — Basic Singleton (Not Thread-Safe)](#v2--basic-singleton-not-thread-safe)
- [V3 — Thread-Safe Singleton (Double-Checked Locking)](#v3--thread-safe-singleton-double-checked-locking)
- [V4 — Sealed Class](#v4--sealed-class)
- [V5 — Singleton + Serialization](#v5--singleton--serialization)
- [V6 — Singleton + Cloning](#v6--singleton--cloning)
- [V7 — Singleton + Reflection](#v7--singleton--reflection)
- [Complete Bulletproof Singleton](#complete-bulletproof-singleton)

---

## What is the Singleton Pattern?

The Singleton pattern is a creational design pattern that ensures a class has **only one instance** throughout the application's lifetime and provides a **global access point** to that instance.

**Core Rules:**
1. Only ONE instance of the class can exist
2. The class controls its own instantiation (private constructor)
3. A public static method provides access to the single instance

**When to use:**
- Database connection pools
- Logger instances
- Configuration managers
- Caches
- Thread pools
- Hardware interface access (printer spooler, device driver)

**When NOT to use:**
- When you need testability (singletons are hard to mock — prefer DI)
- When the "single instance" requirement isn't real (YAGNI)
- When you're using it as a global variable disguised as a pattern

---

## Singleton vs Static Class

| Aspect | Singleton | Static Class |
|--------|-----------|--------------|
| Instance | One object on the heap | No instance — just methods |
| Inheritance | Can implement interfaces, inherit from base class | Cannot inherit or implement interfaces |
| Polymorphism | Yes — can be passed as interface reference | No — static methods can't be virtual |
| Lazy initialization | Yes — created when first accessed | No — loaded when class is first referenced |
| Dependency Injection | Yes — can be registered in DI container | No — hard dependency everywhere |
| Serialization | Can be serialized/deserialized | Cannot |
| State lifetime | Controlled — can be reset or disposed | Lives for entire AppDomain lifetime |
| Testing | Can be mocked via interface | Cannot be mocked (no interface) |
| Thread safety | You control it | Each method must be independently thread-safe |
| Memory | Heap allocated, GC eligible (if reference lost) | Static memory, never collected |

**Rule of thumb:**
- Use **static class** for stateless utility methods (`Math.Max`, `Path.Combine`)
- Use **Singleton** when you need a single instance with state, that participates in OOP (interfaces, DI, polymorphism)

```csharp
// Static class — no state, just utility methods
public static class MathHelper
{
    public static int Add(int a, int b) => a + b;
}

// Singleton — has state, implements interface, injectable
public sealed class AppConfiguration : IConfiguration
{
    private static AppConfiguration? _instance;
    public string Theme { get; set; }
    
    private AppConfiguration() { }
    
    public static AppConfiguration GetInstance() { ... }
}
```

---

## V1 — Why Do We Need Singleton?

**Problem:** Without Singleton, every `new` creates a separate object with its own state.

```csharp
public class AppConfiguration
{
    private readonly Dictionary<string, string> _settings = new();

    public void Set(string key, string value) => _settings[key] = value;
    public string? Get(string key) => _settings.TryGetValue(key, out var val) ? val : null;
}
```

```csharp
var config1 = new AppConfiguration();
config1.Set("Theme", "Dark");

var config2 = new AppConfiguration();
config2.Set("Theme", "Light");

Console.WriteLine(ReferenceEquals(config1, config2)); // False — two separate objects!
```

Module A reads `config1` (Dark), Module B reads `config2` (Light) — they see different values for the same setting. There's no single source of truth.

**Scenarios where Singleton is needed:**
- Database Connection Pool — one pool shared across all services
- Logger — one logger with a consistent file handle
- Configuration Manager — one source of truth for app settings
- Cache — one shared cache, not duplicated per module
- Thread Pool — controlled number of threads, centrally managed

---

## V2 — Basic Singleton (Not Thread-Safe)

The simplest singleton implementation. Works in single-threaded contexts but **breaks under concurrency**.

```csharp
public class AppConfiguration
{
    private static AppConfiguration? _instance;

    // Private constructor — prevents external instantiation
    private AppConfiguration()
    {
        Console.WriteLine("  [Constructor] AppConfiguration instance created.");
    }

    // Public access point
    public static AppConfiguration GetInstance()
    {
        if (_instance == null)
        {
            _instance = new AppConfiguration();
        }
        return _instance;
    }

    private readonly Dictionary<string, string> _settings = new();

    public void Set(string key, string value) => _settings[key] = value;
    public string? Get(string key) => _settings.TryGetValue(key, out var val) ? val : null;
}
```

**Why it's NOT thread-safe:**

```
Thread A: _instance == null? → YES → enters if block
Thread B: _instance == null? → YES → enters if block (before A finishes)
Thread A: creates instance #1
Thread B: creates instance #2 → TWO instances exist!
```

The `null` check and assignment are **not atomic**. Multiple threads can pass the check before any of them assigns the field.

---

## V3 — Thread-Safe Singleton (Double-Checked Locking)

```csharp
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
        // First check: fast path — no lock if already initialized
        if (_instance == null)
        {
            lock (_lock)
            {
                // Second check: inside the lock, verify again
                if (_instance == null)
                {
                    _instance = new AppConfiguration();
                }
            }
        }
        return _instance;
    }

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
```

**Why do we need the FIRST check (outside the lock)?**

Without it, EVERY call to `GetInstance()` acquires the lock — even after the instance is already created. Locks are expensive (kernel transitions, memory barriers, thread scheduling). The first check is a fast path: once `_instance` is set, all subsequent calls skip the lock entirely.

**Why do we need the SECOND check (inside the lock)?**

```
Thread A: first check → null → waits for lock
Thread B: first check → null → waits for lock
Thread A: acquires lock → creates instance → releases lock
Thread B: acquires lock → WITHOUT second check, creates ANOTHER instance!

With second check:
Thread B: acquires lock → _instance == null? NO → returns existing instance ✓
```

Between Thread B's first check and acquiring the lock, Thread A already created the instance. The second check catches this.

**Performance profile:**
- First call: pays lock cost (once)
- All subsequent calls: just a null check — no lock, near-zero overhead

---

## V4 — Sealed Class

```csharp
public sealed class AppConfiguration
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

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
```

**Why does it need to be sealed?**

A private constructor *mostly* prevents inheritance — but not completely:

1. **Nested class attack:** A class nested inside the singleton has access to the private constructor:
   ```csharp
   public class AppConfiguration
   {
       private AppConfiguration() { }
       
       // This nested class CAN access the private constructor!
       private class Sneaky : AppConfiguration { }
       
       public static AppConfiguration CreateAnother() => new Sneaky();
   }
   ```

2. **Reflection:** Can bypass access modifiers entirely (addressed in V7).

3. **Intent communication:** `sealed` makes it explicit to other developers — "this class is complete, don't extend it."

4. **JIT optimization:** The JIT can devirtualize method calls on sealed types, giving a minor performance benefit.

**`sealed` + private constructor = airtight against inheritance.**

---

## V5 — Singleton + Serialization

**Problem:** Deserialization creates a NEW instance — bypassing the private constructor.

```
1. Serialize singleton → JSON/bytes
2. Deserialize → deserializer creates a NEW object
3. Now TWO instances exist → Singleton broken!
```

**Solution:** Custom `JsonConverter` that returns the existing singleton instead of creating a new object.

```csharp
public sealed class AppConfiguration
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
}
```

```csharp
public class SingletonJsonConverter : JsonConverter<AppConfiguration>
{
    public override AppConfiguration Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Get the EXISTING singleton — don't create a new instance
        var singleton = AppConfiguration.GetInstance();

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("Theme", out var theme))
            singleton.Theme = theme.GetString() ?? "Default";

        if (root.TryGetProperty("MaxConnections", out var maxConn))
            singleton.MaxConnections = maxConn.GetInt32();

        // Return the SAME singleton — no new object created
        return singleton;
    }

    public override void Write(
        Utf8JsonWriter writer, AppConfiguration value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Theme", value.Theme);
        writer.WriteNumber("MaxConnections", value.MaxConnections);
        writer.WriteEndObject();
    }
}
```

**Usage:**
```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new SingletonJsonConverter());

string json = """{"Theme":"Ocean","MaxConnections":100}""";
var deserialized = JsonSerializer.Deserialize<AppConfiguration>(json, options);

ReferenceEquals(AppConfiguration.GetInstance(), deserialized); // True ✓
```

---

## V6 — Singleton + Cloning

**Problem:** `ICloneable` or `MemberwiseClone()` creates a copy — a second instance.

```csharp
var original = AppConfiguration.GetInstance();
var clone = (AppConfiguration)original.Clone(); // NEW object!
ReferenceEquals(original, clone); // False → Singleton broken!
```

**Solution:** Make `Clone()` return the same instance.

```csharp
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

    // SAFE: Returns the same singleton instance
    public object Clone()
    {
        return GetInstance();

        // Alternative: throw to make misuse obvious
        // throw new InvalidOperationException(
        //     "Singleton cannot be cloned. Use GetInstance() instead.");
    }
}
```

**Protection strategies:**
1. Don't implement `ICloneable` at all (best — no Clone method = no cloning)
2. `Clone()` returns `GetInstance()` (safe — always returns the singleton)
3. `Clone()` throws `InvalidOperationException` (fail-fast on misuse)
4. `sealed` prevents a subclass from adding its own Clone method

---

## V7 — Singleton + Reflection

**Problem:** Reflection can bypass the private constructor and create new instances.

```csharp
var ctor = typeof(AppConfiguration)
    .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
var newInstance = (AppConfiguration)ctor!.Invoke(null);
// newInstance != GetInstance() → Singleton broken!
```

**Solution:** Guard inside the constructor — if an instance already exists, throw.

```csharp
public sealed class AppConfiguration
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

    public string Theme { get; set; } = "Default";

    private AppConfiguration()
    {
        // GUARD: Prevents reflection from creating a second instance
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
```

**What happens when reflection tries to attack:**
```csharp
var instance = AppConfiguration.GetInstance(); // Normal — works fine

// Reflection attack:
var ctor = typeof(AppConfiguration).GetConstructor(
    BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
ctor!.Invoke(null); // THROWS InvalidOperationException ✓
```

**Alternative: `Lazy<T>` based singleton (simplest thread-safe + reflection-guarded):**

```csharp
public sealed class AppConfiguration
{
    private static bool _instantiated;

    private static readonly Lazy<AppConfiguration> _lazy = new(() =>
    {
        _instantiated = true;
        return new AppConfiguration();
    });

    public static AppConfiguration Instance => _lazy.Value;

    private AppConfiguration()
    {
        if (_instantiated)
        {
            throw new InvalidOperationException(
                "Singleton violation! Use AppConfiguration.Instance instead.");
        }
    }
}
```

`Lazy<T>` gives you thread-safe initialization for free — no manual double-checked locking needed.

---

## Complete Bulletproof Singleton

All defenses combined:

```csharp
public sealed class AppConfiguration : ICloneable
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

    public string Theme { get; set; } = "Default";
    public int MaxConnections { get; set; } = 10;

    private AppConfiguration()
    {
        // V7: Reflection guard
        if (_instance != null)
        {
            throw new InvalidOperationException(
                "Singleton violation! Use AppConfiguration.GetInstance() instead.");
        }
    }

    // V3: Double-checked locking (thread-safe)
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

    // V6: Clone returns same instance
    public object Clone() => GetInstance();

    // V5: Custom deserialization would use SingletonJsonConverter
    // (registered in JsonSerializerOptions, not shown here for brevity)
}

// V4: sealed prevents inheritance
// V2: private constructor prevents new AppConfiguration()
// V3: double-checked lock prevents race conditions
// V5: custom converter prevents deserialization bypass
// V6: Clone() returns this prevents cloning bypass
// V7: constructor guard prevents reflection bypass
```

| Version | Defense | Threat |
|---------|---------|--------|
| V2 | Private constructor | `new AppConfiguration()` |
| V3 | Double-checked locking | Race condition (multi-threading) |
| V4 | `sealed` | Subclass creating instances |
| V5 | Custom `JsonConverter` | Deserialization creating new object |
| V6 | `Clone()` returns `this` | `ICloneable` / `MemberwiseClone` |
| V7 | Constructor guard + throw | Reflection invoking private constructor |
