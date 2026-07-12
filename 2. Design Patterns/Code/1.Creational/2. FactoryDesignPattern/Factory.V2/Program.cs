using Factory.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT FACTORY PATTERN
// =============================================================================

Console.WriteLine("=== With Factory: Clean separation ===");
Console.WriteLine();

var factory = new NotificationFactory();
var service = new NotificationService(factory);

// Client only knows about the enum — not the concrete classes
service.Notify(NotificationType.Email, "Your order has shipped!");
service.Notify(NotificationType.Sms, "OTP: 482910");
service.Notify(NotificationType.Push, "New message from Alice");

Console.WriteLine();
Console.WriteLine("=== Notify all channels ===");
service.NotifyAll("System maintenance at 2 AM");

Console.WriteLine();
Console.WriteLine("=== Using factory directly ===");
INotification notification = factory.CreateNotification(NotificationType.Email);
notification.Send("Direct factory usage");

Console.WriteLine();
Console.WriteLine("=== Benefits achieved ===");
Console.WriteLine("1. Client (NotificationService) has ZERO coupling to concrete classes");
Console.WriteLine("2. Adding WhatsAppNotification = new class + one factory case. Client unchanged.");
Console.WriteLine("3. Factory can be injected via DI → testable with mocks");
Console.WriteLine("4. Creation logic is centralized in one place");
Console.WriteLine("5. Open/Closed Principle: open for extension, closed for modification");
