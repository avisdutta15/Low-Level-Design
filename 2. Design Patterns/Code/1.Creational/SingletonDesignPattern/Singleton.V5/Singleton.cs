using System.Text.Json;
using System.Text.Json.Serialization;

namespace Singleton.V5;

// =============================================================================
// V5: SINGLETON + SERIALIZATION
// =============================================================================
//
// PROBLEM:
// Serialization (JSON, Binary, XML) can BREAK the Singleton guarantee.
//
// When you serialize a singleton and then deserialize it, the deserializer
// creates a NEW instance — bypassing the private constructor entirely.
// Now you have TWO objects that both claim to be "the" singleton.
//
// Flow:
//   1. Get singleton instance → Instance A (the real one)
//   2. Serialize Instance A → JSON/bytes
//   3. Deserialize → Instance B (a NEW object created by the deserializer)
//   4. Instance A != Instance B → Singleton contract violated!
//
// SOLUTION:
// Override the deserialization behavior to return the existing singleton instance
// instead of creating a new one.
//
// For System.Text.Json: Use a custom JsonConverter.
// For BinaryFormatter (legacy): Implement ISerializable + GetObjectData.
// For Newtonsoft.Json: Use a custom JsonConverter or ISerializationCallback.
// =============================================================================

public sealed class AppConfiguration
{
    private static AppConfiguration? _instance;
    private static readonly object _lock = new();

    // Properties must have setters for deserialization to work,
    // but we control what happens via the custom converter
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

    /// <summary>
    /// Resets the singleton for demonstration purposes only.
    /// </summary>
    internal static void ResetForDemo() => _instance = null;
}

// =============================================================================
// Custom JsonConverter that preserves Singleton on deserialization
// =============================================================================

public class SingletonJsonConverter : JsonConverter<AppConfiguration>
{
    public override AppConfiguration Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Instead of creating a new instance, get the existing singleton
        var singleton = AppConfiguration.GetInstance();

        // Read the JSON and apply values to the EXISTING instance
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("Theme", out var theme))
            singleton.Theme = theme.GetString() ?? "Default";

        if (root.TryGetProperty("MaxConnections", out var maxConn))
            singleton.MaxConnections = maxConn.GetInt32();

        // Return the SAME singleton instance — no new object created
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
