using Factory.V1;

// =============================================================================
// V1: WHY DO WE NEED THE FACTORY PATTERN?
// =============================================================================
//
// Problem: Client code is tightly coupled to concrete implementations.
// When the client uses `new` directly, it must know the exact class to
// instantiate — this violates the Open/Closed Principle and makes the
// code rigid, hard to extend, and difficult to test.
// =============================================================================

Console.WriteLine("=== Without Factory: The Problem ===");
Console.WriteLine();

// The client must know EVERY concrete class and decide which one to create.
// If we add a new notification type, we must modify EVERY place that creates notifications.

string userChoice = "email";

INotification notification;

// This switch/if block is duplicated everywhere a notification is needed
if (userChoice == "email")
    notification = new EmailNotification();
else if (userChoice == "sms")
    notification = new SmsNotification();
else if (userChoice == "push")
    notification = new PushNotification();
else
    throw new ArgumentException($"Unknown notification type: {userChoice}");

notification.Send("Hello from V1!");

Console.WriteLine();
Console.WriteLine("=== Problems with this approach ===");
Console.WriteLine("1. Client is tightly coupled to concrete classes (knows EmailNotification, SmsNotification, etc.)");
Console.WriteLine("2. Adding a new type (e.g., WhatsApp) requires changing EVERY place that creates notifications");
Console.WriteLine("3. Violates Open/Closed Principle — not open for extension without modification");
Console.WriteLine("4. Violates Single Responsibility — client both creates AND uses objects");
Console.WriteLine("5. Hard to unit test — can't mock the creation logic");
Console.WriteLine("6. Conditional logic (if/switch) scattered across the codebase");
