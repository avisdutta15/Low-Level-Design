# Factory Design Pattern

## Table of Contents

- [What is the Factory Pattern?](#what-is-the-factory-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Factory?](#v1--why-do-we-need-factory)
- [V2 — How to Implement Factory](#v2--how-to-implement-factory)
- [When to Use Factory](#when-to-use-factory)

---

## What is the Factory Pattern?

The Factory pattern is a **creational design pattern** that provides an interface for creating objects without exposing the instantiation logic to the client. Instead of the client calling `new ConcreteClass()` directly, it delegates creation to a factory method or factory class.

**Core Idea:**
- The client asks the factory for an object by specifying a type/parameter
- The factory decides which concrete class to instantiate
- The client receives the object through an abstraction (interface/base class)
- The client never knows or cares which concrete class was used

**Key Principles Satisfied:**
- **Open/Closed Principle:** Add new types without modifying existing client code
- **Single Responsibility:** Creation logic is separated from business logic
- **Dependency Inversion:** Client depends on abstractions, not concrete classes

---

## UML Diagram

```
┌─────────────────────────────┐
│         «interface»         │
│        INotification        │
├─────────────────────────────┤
│ + Send(message: string)     │
└─────────────┬───────────────┘
              │ implements
              │
    ┌─────────┼──────────┐
    │         │          │
    ▼         ▼          ▼
┌────────┐ ┌───────┐ ┌────────┐
│ Email  │ │  SMS  │ │  Push  │
│Notifi- │ │Notifi-│ │Notifi- │
│cation  │ │cation │ │cation  │
├────────┤ ├───────┤ ├────────┤
│+Send() │ │+Send()│ │+Send() │
└────────┘ └───────┘ └────────┘
    ▲         ▲          ▲
    │         │          │
    └─────────┼──────────┘
              │ creates
              │
┌─────────────┴───────────────┐
│    NotificationFactory      │
├─────────────────────────────┤
│ + CreateNotification(       │
│     type: NotificationType  │
│   ): INotification          │
└─────────────┬───────────────┘
              │
              │ uses
              ▼
┌─────────────────────────────┐
│    NotificationService      │
│         (Client)            │
├─────────────────────────────┤
│ - _factory: Notification-   │
│             Factory         │
├─────────────────────────────┤
│ + Notify(type, message)     │
│ + NotifyAll(message)        │
└─────────────────────────────┘
```

**Relationships:**
- `NotificationService` (Client) → depends on → `NotificationFactory` and `INotification`
- `NotificationFactory` → creates → concrete `INotification` implementations
- Client **never** depends on `EmailNotification`, `SmsNotification`, or `PushNotification` directly

---

## V1 — Why Do We Need Factory?

**The Problem: Client tightly coupled to concrete classes.**

```csharp
string userChoice = "email";

INotification notification;

// This if/switch is duplicated EVERYWHERE a notification is created
if (userChoice == "email")
    notification = new EmailNotification();
else if (userChoice == "sms")
    notification = new SmsNotification();
else if (userChoice == "push")
    notification = new PushNotification();
else
    throw new ArgumentException($"Unknown notification type: {userChoice}");

notification.Send("Hello!");
```

**What's wrong here:**

| Problem | Explanation |
|---------|-------------|
| Tight coupling | Client knows `EmailNotification`, `SmsNotification`, `PushNotification` by name |
| Violates Open/Closed | Adding `WhatsAppNotification` requires changing every file that creates notifications |
| Violates SRP | Client is responsible for both creation logic AND business logic |
| Code duplication | The same switch/if block is repeated across the codebase |
| Hard to test | Can't mock creation — the `new` calls are hardcoded |
| Shotgun surgery | One new type → changes scattered across many files |

**The classes themselves are fine — it's the CREATION that's the problem.**

```csharp
public interface INotification
{
    void Send(string message);
}

public class EmailNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"[Email] Sending: {message}");
}

public class SmsNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"[SMS] Sending: {message}");
}

public class PushNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"[Push] Sending: {message}");
}
```

---

## V2 — How to Implement Factory

**Step 1: Define the Product interface**

```csharp
public interface INotification
{
    void Send(string message);
}
```

**Step 2: Create concrete products**

```csharp
public class EmailNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"[Email] Sending: {message}");
}

public class SmsNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"[SMS] Sending: {message}");
}

public class PushNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"[Push] Sending: {message}");
}
```

**Step 3: Create the Factory**

```csharp
public enum NotificationType
{
    Email,
    Sms,
    Push
}

public class NotificationFactory
{
    public INotification CreateNotification(NotificationType type)
    {
        return type switch
        {
            NotificationType.Email => new EmailNotification(),
            NotificationType.Sms => new SmsNotification(),
            NotificationType.Push => new PushNotification(),
            _ => throw new ArgumentException($"Unknown notification type: {type}")
        };
    }
}
```

**Step 4: Client uses factory (never touches concrete classes)**

```csharp
public class NotificationService
{
    private readonly NotificationFactory _factory;

    public NotificationService(NotificationFactory factory)
    {
        _factory = factory;
    }

    public void Notify(NotificationType type, string message)
    {
        INotification notification = _factory.CreateNotification(type);
        notification.Send(message);
    }

    public void NotifyAll(string message)
    {
        foreach (NotificationType type in Enum.GetValues<NotificationType>())
        {
            INotification notification = _factory.CreateNotification(type);
            notification.Send(message);
        }
    }
}
```

**Step 5: Usage**

```csharp
var factory = new NotificationFactory();
var service = new NotificationService(factory);

service.Notify(NotificationType.Email, "Your order has shipped!");
service.Notify(NotificationType.Sms, "OTP: 482910");
service.Notify(NotificationType.Push, "New message from Alice");

service.NotifyAll("System maintenance at 2 AM");
```

**Adding a new notification type (e.g., WhatsApp):**

```csharp
// 1. New class — implements existing interface
public class WhatsAppNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"[WhatsApp] Sending: {message}");
}

// 2. New enum value
public enum NotificationType { Email, Sms, Push, WhatsApp }

// 3. One new case in factory
NotificationType.WhatsApp => new WhatsAppNotification(),

// 4. Client code (NotificationService) is COMPLETELY UNCHANGED ✓
```

---

## When to Use Factory

| Use Factory When | Don't Use Factory When |
|------------------|------------------------|
| Object creation involves logic/decisions | Only one implementation exists |
| Multiple implementations of same interface | Object creation is trivial (`new Config()`) |
| Client shouldn't know concrete classes | You're adding indirection for no benefit |
| You need to centralize creation for consistency | The pattern adds complexity without solving a problem |
| You want testability (mock the factory) | YAGNI — you don't actually need extension |

**Real-world examples:**
- `DbProviderFactory` in ADO.NET — creates connections, commands without knowing SqlServer vs Postgres
- `LoggerFactory` in Microsoft.Extensions.Logging — creates loggers without knowing Console vs File vs Seq
- `HttpClientFactory` — creates configured HttpClient instances
- Game engines — `EnemyFactory.Create(EnemyType.Boss)` without knowing Boss implementation details
