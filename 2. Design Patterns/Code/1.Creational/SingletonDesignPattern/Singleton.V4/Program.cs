using Singleton.V4;

// =============================================================================
// V4 DEMO: Sealed Singleton — prevents subclassing
// =============================================================================

Console.WriteLine("=== Sealed Singleton ===");
var instance = AppConfiguration.GetInstance();
instance.Set("Mode", "Production");

Console.WriteLine($"Mode: {instance.Get("Mode")}");
Console.WriteLine($"Type: {instance.GetType().Name}");
Console.WriteLine($"Is sealed? {instance.GetType().IsSealed}"); // True

Console.WriteLine();
Console.WriteLine("=== Why sealed matters ===");
Console.WriteLine("Try uncommenting the MaliciousConfig class in Singleton.cs");
Console.WriteLine("You'll get: error CS0509: cannot derive from sealed type 'AppConfiguration'");
Console.WriteLine();
Console.WriteLine("Without sealed, a nested class could do:");
Console.WriteLine("  public class AppConfiguration {");
Console.WriteLine("      private class Sneaky : AppConfiguration { }  // has access to private ctor!");
Console.WriteLine("      public static AppConfiguration CreateAnother() => new Sneaky();");
Console.WriteLine("  }");
Console.WriteLine();
Console.WriteLine("sealed + private constructor = no subclassing, no extra instances.");
