using System.Collections.Concurrent;

// Notification System V1
//
// Problem Statement:
//   A Notification System informs users about events (new messages, payment updates,
//   reminders, alerts) via multiple channels (Email, SMS, Push).
//
// Core Entities:
//   Notification
//      - UserId, Message
//   UserPreference
//      - UserId
//      - PreferredChannels (set of ChannelType)
//   NotificationChannel (interface)
//      - Send(notification)
//      - Concrete: EmailChannel, SmsChannel, PushChannel
//   NotificationChannelFactory
//      - GetChannel(channelType) → returns the appropriate channel instance
//   UserPreferenceService
//      - Stores and retrieves user preferences (thread-safe via ConcurrentDictionary)
//   NotificationDispatcher
//      - Gets user preferences, iterates preferred channels, dispatches via factory
//   NotificationService (synchronous)
//      - Delegates to dispatcher
//   AsyncNotificationService (asynchronous)
//      - Dispatches via Task.Run for non-blocking delivery
//
// Overall Flow:
//   NotificationService.SendNotification(notification)
//     → NotificationDispatcher.Dispatch(notification)
//       → UserPreferenceService.GetPreference(userId)
//       → For each preferred channel:
//           → NotificationChannelFactory.GetChannel(channelType)
//           → channel.Send(notification)

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum ChannelType
{
    Email,
    Sms,
    Push
}

// ─────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────
public class Notification
{
    public string UserId { get; }
    public string Message { get; }

    public Notification(string userId, string message)
    {
        UserId = userId;
        Message = message;
    }
}

public class UserPreference
{
    public string UserId { get; }
    public HashSet<ChannelType> PreferredChannels { get; }

    public UserPreference(string userId, HashSet<ChannelType> preferredChannels)
    {
        UserId = userId;
        PreferredChannels = preferredChannels;
    }
}

// ─────────────────────────────────────────────
// Channel Interface + Implementations
// ─────────────────────────────────────────────
public interface INotificationChannel
{
    // Returns true if sent successfully, false on failure
    bool Send(Notification notification);
}

public class EmailNotificationChannel : INotificationChannel
{
    public bool Send(Notification notification)
    {
        Console.WriteLine($"    Sending EMAIL to user {notification.UserId}: {notification.Message}");
        return true;
    }
}

public class SmsNotificationChannel : INotificationChannel
{
    public bool Send(Notification notification)
    {
        Console.WriteLine($"    Sending SMS to user {notification.UserId}: {notification.Message}");
        return true;
    }
}

public class PushNotificationChannel : INotificationChannel
{
    public bool Send(Notification notification)
    {
        Console.WriteLine($"    Sending PUSH to user {notification.UserId}: {notification.Message}");
        return true;
    }
}

// A channel that simulates intermittent failure for demo
public class FlakyEmailChannel : INotificationChannel
{
    private int _attempts = 0;

    public bool Send(Notification notification)
    {
        _attempts++;
        if (_attempts < 3)
        {
            Console.WriteLine($"    [Email] FAILED attempt {_attempts} for {notification.UserId}");
            return false;
        }
        Console.WriteLine($"    [Email] Sent to {notification.UserId}: {notification.Message} (attempt {_attempts})");
        _attempts = 0; // reset for next notification
        return true;
    }
}

// ─────────────────────────────────────────────
// Factory
// ─────────────────────────────────────────────
public static class NotificationChannelFactory
{
    public static INotificationChannel GetChannel(ChannelType channelType)
    {
        return channelType switch
        {
            ChannelType.Email => new EmailNotificationChannel(),
            ChannelType.Sms => new SmsNotificationChannel(),
            ChannelType.Push => new PushNotificationChannel(),
            _ => throw new ArgumentException($"Unknown channel type: {channelType}")
        };
    }
}

// ─────────────────────────────────────────────
// UserPreferenceService
// ─────────────────────────────────────────────
public class UserPreferenceService
{
    private readonly ConcurrentDictionary<string, UserPreference> _preferences = new();

    public void SavePreference(UserPreference preference)
    {
        _preferences[preference.UserId] = preference;
    }

    public UserPreference GetPreference(string userId)
    {
        return _preferences.GetOrAdd(userId,
            _ => new UserPreference(userId, new HashSet<ChannelType> { ChannelType.Email }));
    }
}

// ─────────────────────────────────────────────
// NotificationDispatcher (with retry)
// ─────────────────────────────────────────────
public class NotificationDispatcher
{
    private readonly UserPreferenceService _preferenceService;
    private readonly int _maxRetries;
    private readonly Dictionary<ChannelType, INotificationChannel>? _channelOverride;

    public NotificationDispatcher(UserPreferenceService preferenceService, int maxRetries = 3,
        Dictionary<ChannelType, INotificationChannel>? channelOverride = null)
    {
        _preferenceService = preferenceService;
        _maxRetries = maxRetries;
        _channelOverride = channelOverride;
    }

    public void Dispatch(Notification notification)
    {
        UserPreference preference = _preferenceService.GetPreference(notification.UserId);

        foreach (ChannelType channelType in preference.PreferredChannels)
        {
            INotificationChannel channel = _channelOverride != null && _channelOverride.ContainsKey(channelType)
                ? _channelOverride[channelType]
                : NotificationChannelFactory.GetChannel(channelType);

            SendWithRetry(channel, channelType, notification);
        }
    }

    private void SendWithRetry(INotificationChannel channel, ChannelType type, Notification notification)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            bool success = channel.Send(notification);
            if (success) return;

            if (attempt < _maxRetries)
                Console.WriteLine($"    [{type}] Retrying... ({attempt}/{_maxRetries})");
        }
        Console.WriteLine($"    [{type}] FAILED after {_maxRetries} retries for {notification.UserId}");
    }
}

// ─────────────────────────────────────────────
// NotificationService (synchronous)
// ─────────────────────────────────────────────
public class NotificationService
{
    private readonly NotificationDispatcher _dispatcher;

    public NotificationService(NotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void SendNotification(Notification notification)
    {
        _dispatcher.Dispatch(notification);
    }
}

// ─────────────────────────────────────────────
// AsyncNotificationService (asynchronous)
// ─────────────────────────────────────────────
public class AsyncNotificationService
{
    private readonly NotificationDispatcher _dispatcher;

    public AsyncNotificationService(NotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task SendNotification(Notification notification)
    {
        return Task.Run(() => _dispatcher.Dispatch(notification));
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static async Task Main(string[] args)
    {
        // Defining preference service.
        UserPreferenceService preferenceService = new UserPreferenceService();

        // Defining user preference with Email and SMS as preferred channels.
        preferenceService.SavePreference(
            new UserPreference("user123", new HashSet<ChannelType> { ChannelType.Email, ChannelType.Sms }));

        // Defining notification dispatcher (with retry, maxRetries=3)
        NotificationDispatcher dispatcher = new NotificationDispatcher(preferenceService, maxRetries: 3);

        // Defining async service.
        AsyncNotificationService asyncService = new AsyncNotificationService(dispatcher);

        // Defining synchronous service.
        NotificationService service = new NotificationService(dispatcher);

        // Defining notification to send through multiple channels.
        Notification notification = new Notification("user123", "Your order has been shipped!");

        // Sending notification through synchronous service.
        Console.WriteLine("=== Synchronous Send ===");
        service.SendNotification(notification);

        // Sending notification through asynchronous service.
        Console.WriteLine("\n=== Asynchronous Send ===");
        await asyncService.SendNotification(notification);

        // Demo: Retry with a flaky email channel
        Console.WriteLine("\n=== Retry Demo (Flaky Email) ===");
        preferenceService.SavePreference(
            new UserPreference("user456", new HashSet<ChannelType> { ChannelType.Email }));

        // Override factory with flaky channel
        var flakyDispatcher = new NotificationDispatcher(preferenceService, maxRetries: 3,
            channelOverride: new Dictionary<ChannelType, INotificationChannel>
            {
                { ChannelType.Email, new FlakyEmailChannel() }
            });

        var flakyService = new NotificationService(flakyDispatcher);
        flakyService.SendNotification(new Notification("user456", "Important security alert!"));
    }
}

// A dispatcher variant that uses FlakyEmailChannel for demo purposes
public class FlakyNotificationDispatcher : NotificationDispatcher
{
    private readonly UserPreferenceService _preferenceService;
    private readonly int _maxRetries;

    public FlakyNotificationDispatcher(UserPreferenceService preferenceService, int maxRetries = 3)
        : base(preferenceService, maxRetries)
    {
        _preferenceService = preferenceService;
        _maxRetries = maxRetries;
    }

    public new void Dispatch(Notification notification)
    {
        UserPreference preference = _preferenceService.GetPreference(notification.UserId);

        foreach (ChannelType channelType in preference.PreferredChannels)
        {
            // Use flaky channel for Email to demonstrate retry
            INotificationChannel channel = channelType == ChannelType.Email
                ? new FlakyEmailChannel()
                : NotificationChannelFactory.GetChannel(channelType);

            SendWithRetry(channel, channelType, notification);
        }
    }

    private void SendWithRetry(INotificationChannel channel, ChannelType type, Notification notification)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            bool success = channel.Send(notification);
            if (success) return;

            if (attempt < _maxRetries)
                Console.WriteLine($"    [{type}] Retrying... ({attempt}/{_maxRetries})");
        }
        Console.WriteLine($"    [{type}] FAILED after {_maxRetries} retries for {notification.UserId}");
    }
}
