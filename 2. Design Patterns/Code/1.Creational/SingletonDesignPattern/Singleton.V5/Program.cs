using System.Text.Json;
using Singleton.V5;

// =============================================================================
// V5 DEMO: Singleton + Serialization
// =============================================================================

Console.WriteLine("=== Problem: Default deserialization breaks Singleton ===");

var instance = AppConfiguration.GetInstance();
instance.Theme = "Dark";
instance.MaxConnections = 50;

// Serialize
string json = JsonSerializer.Serialize(instance);
Console.WriteLine($"Serialized: {json}");

// Deserialize WITHOUT custom converter — creates a NEW instance!
// (This would break singleton if we used it directly)
Console.WriteLine();
Console.WriteLine("Without custom converter, JsonSerializer.Deserialize<T>() calls");
Console.WriteLine("the parameterless constructor (or uses reflection) → new object!");

Console.WriteLine();
Console.WriteLine("=== Solution: Custom JsonConverter preserves Singleton ===");

var options = new JsonSerializerOptions();
options.Converters.Add(new SingletonJsonConverter());

// Simulate receiving JSON from a file/network
string incomingJson = """{"Theme":"Ocean","MaxConnections":100}""";
Console.WriteLine($"Incoming JSON: {incomingJson}");

// Deserialize WITH custom converter — returns the EXISTING singleton
var deserialized = JsonSerializer.Deserialize<AppConfiguration>(incomingJson, options);

Console.WriteLine();
Console.WriteLine($"Same instance? {ReferenceEquals(instance, deserialized)}"); // True!
Console.WriteLine($"Theme updated: {instance.Theme}"); // Ocean
Console.WriteLine($"MaxConnections updated: {instance.MaxConnections}"); // 100

Console.WriteLine();
Console.WriteLine("=== Key Takeaway ===");
Console.WriteLine("The custom converter reads JSON values into the EXISTING singleton");
Console.WriteLine("instead of letting the deserializer create a new object.");
