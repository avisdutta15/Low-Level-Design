using System.Collections.Concurrent;

// Notification System V3
//
// Extends V2 with:
// 1. Message Templates with Placeholders
//      - NotificationTemplate holds a template string with {{placeholders}}
//      - Render(context) replaces placeholders with actual values
//      - Example: "Hello {{name}}, your order {{orderId}} has shipped!"
//      - Templates are reusable across notifications
//
// 2. Per-User Channel Preferences and Opt-Out
//      - UserPreference now has:
//          - PreferredChannels: which channels the user wants
//          - OptedOutChannels: channels the user has explicitly disabled
//      - Dispatcher skips opted-out channels even if they're in preferred list
//      - Users can opt-out per channel without unsubscribing entirely
//
// Design:
//   NotificationTemplate
//      - TemplateString (e.g., "Hello {{name}}, {{message}}")
//      - Render(Dictionary<string, string> context) → resolved string
//   UserPreference
//      - PreferredChannels (what they want)
//      - OptedOutChannels (what they've disabled)
//      - GetActiveChannels() → PreferredChannels minus OptedOutChannels
//   NotificationDispatcher
//      - Uses template to resolve message before sending
//      - Checks active channels (respects opt-out)
//      - Applies per-channel retry policy (same as V2)
//
// Overall Flow:
//   service.SendNotification(notification, template, context)
//     → template.Render(context) → resolved message
//     → dispatcher.Dispatch(notification with resolved message)
//       → userPreference.GetActiveChannels() (preferred minus opt-out)
//       → for each active channel: retryPolicy.Execute(channel.Send)

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

// ─────────────────────────────────────────────
// Message Template (Feature 1)
// ─────────────────────────────────────────────
public class NotificationTemplate
{
    public string Name { get; }
    public string TemplateString { get; }

    public NotificationTemplate(string name, string templateString)
    {
        Name = name;
        TemplateString = templateString;
    }

    // Replaces {{key}} placeholders with values from context
    public string Render(Dictionary<string, string> context)
    {
        string result = TemplateString;
        foreach (var (key, value) in context)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }
}

// ─────────────────────────────────────────────
// User Preference (Feature 2: with Opt-Out)
// ─────────────────────────────────────────────
public class UserPreference
{
    public string UserId { get; }
    public HashSet<ChannelType> PreferredChannels { get; }
    public HashSet<ChannelType> OptedOutChannels { get; }

    public UserPreference(string userId, HashSet<ChannelType> preferredChannels,
        HashSet<ChannelType>? optedOutChannels = null)
    {
        UserId = userId;
        PreferredChannels = preferredChannels;
        OptedOutChannels = optedOutChannels ?? new HashSet<ChannelType>();
    }

    // Returns channels that are preferred AND not opted-out
    public HashSet<ChannelType> GetActiveChannels()
    {
        var active = new HashSet<ChannelType>(PreferredChannels);
        active.ExceptWith(OptedOutChannels);
        return active;
    }

    public void OptOut(ChannelType channel) => OptedOutChannels.Add(channel);
    public void OptIn(ChannelType channel) => OptedOutChannels.Remove(channel);
}

// ─────────────────────────────────────────────
// Retry Policy (Strategy Pattern — same as V2)
// ─────────────────────────────────────────────
public interface IRetryPolicy
{
    bool Execute(Func<bool> action, string context);
}

public class NoRetryPolicy : IRetryPolicy
{
    public bool Execute(Func<bool> action, string context)
    {
        bool success = action();
        if (!success)
            Console.WriteLine($"    [{context}] Failed. No retry configured.");
        return success;
    }
}

public class SimpleRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;

    public SimpleRetryPolicy(int maxRetries = 3) => _maxRetries = maxRetries;

    public bool Execute(Func<bool> action, string context)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            if (action()) return true;
            if (attempt < _maxRetries)
                Console.WriteLine($"    [{context}] Retrying... ({attempt}/{_maxRetries})");
        }
        Console.WriteLine($"    [{context}] FAILED after {_maxRetries} retries.");
        return false;
    }
}

public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly int _baseDelayMs;

    public ExponentialBackoffRetryPolicy(int maxRetries = 3, int baseDelayMs = 50)
    {
        _maxRetries = maxRetries;
        _baseDelayMs = baseDelayMs;
    }

    public bool Execute(Func<bool> action, string context)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            if (action()) return true;
            if (attempt < _maxRetries)
            {
                int delay = _baseDelayMs * (int)Math.Pow(2, attempt - 1);
                Console.WriteLine($"    [{context}] Failed. Backing off {delay}ms... ({attempt}/{_maxRetries})");
                Thread.Sleep(delay);
            }
        }
        Console.WriteLine($"    [{context}] FAILED after {_maxRetries} retries with backoff.");
        return false;
    }
}

// ─────────────────────────────────────────────
// Channel Interface + Implementations
// ─────────────────────────────────────────────
public interface INotificationChannel
{
    bool Send(Notification notification);
}

public class EmailNotificationChannel : INotificationChannel
{
    public bool Send(Notification notification)
    {
        Console.WriteLine($"    [Email] Sent to {notification.UserId}: {notification.Message}");
        return true;
    }
}

public class SmsNotificationChannel : INotificationChannel
{
    public bool Send(Notification notification)
    {
        Console.WriteLine($"    [SMS] Sent to {notification.UserId}: {notification.Message}");
        return true;
    }
}

public class PushNotificationChannel : INotificationChannel
{
    public bool Send(Notification notification)
    {
        Console.WriteLine($"    [Push] Sent to {notification.UserId}: {notification.Message}");
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
// NotificationDispatcher
// ─────────────────────────────────────────────
public class NotificationDispatcher
{
    private readonly UserPreferenceService _preferenceService;
    private readonly Dictionary<ChannelType, IRetryPolicy> _retryPolicies;
    private readonly IRetryPolicy _defaultPolicy;

    public NotificationDispatcher(
        UserPreferenceService preferenceService,
        Dictionary<ChannelType, IRetryPolicy> retryPolicies)
    {
        _preferenceService = preferenceService;
        _retryPolicies = retryPolicies;
        _defaultPolicy = new SimpleRetryPolicy(3);
    }

    public void Dispatch(Notification notification)
    {
        UserPreference preference = _preferenceService.GetPreference(notification.UserId);

        // Get active channels (preferred minus opted-out)
        HashSet<ChannelType> activeChannels = preference.GetActiveChannels();

        if (activeChannels.Count == 0)
        {
            Console.WriteLine($"    No active channels for {notification.UserId} (all opted-out)");
            return;
        }

        foreach (ChannelType channelType in activeChannels)
        {
            INotificationChannel channel = NotificationChannelFactory.GetChannel(channelType);
            IRetryPolicy policy = _retryPolicies.ContainsKey(channelType)
                ? _retryPolicies[channelType]
                : _defaultPolicy;

            policy.Execute(() => channel.Send(notification), channelType.ToString());
        }
    }
}

// ─────────────────────────────────────────────
// NotificationService
// ─────────────────────────────────────────────
public class NotificationService
{
    private readonly NotificationDispatcher _dispatcher;

    public NotificationService(NotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    // Send with a raw message
    public void SendNotification(Notification notification)
    {
        _dispatcher.Dispatch(notification);
    }

    // Send with a template + context (resolves placeholders before dispatch)
    public void SendNotification(string userId, NotificationTemplate template, Dictionary<string, string> context)
    {
        string resolvedMessage = template.Render(context);
        Console.WriteLine($"  → Resolved: \"{resolvedMessage}\"");
        _dispatcher.Dispatch(new Notification(userId, resolvedMessage));
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        UserPreferenceService preferenceService = new UserPreferenceService();

        // Retry policies per channel (same as V2)
        var retryPolicies = new Dictionary<ChannelType, IRetryPolicy>
        {
            { ChannelType.Email, new ExponentialBackoffRetryPolicy(maxRetries: 3, baseDelayMs: 50) },
            { ChannelType.Sms, new SimpleRetryPolicy(maxRetries: 3) },
            { ChannelType.Push, new NoRetryPolicy() }
        };

        var dispatcher = new NotificationDispatcher(preferenceService, retryPolicies);
        var service = new NotificationService(dispatcher);

        // ── Feature 1: Message Templates ──
        Console.WriteLine("=== Feature 1: Message Templates ===\n");

        var orderTemplate = new NotificationTemplate("order_shipped",
            "Hello {{name}}, your order #{{orderId}} has been shipped! Track at {{trackingUrl}}");

        var otpTemplate = new NotificationTemplate("otp",
            "Hi {{name}}, your OTP is {{otp}}. Valid for {{expiry}} minutes.");

        // User with all channels active
        preferenceService.SavePreference(
            new UserPreference("user1", new HashSet<ChannelType> { ChannelType.Email, ChannelType.Sms, ChannelType.Push }));

        service.SendNotification("user1", orderTemplate, new Dictionary<string, string>
        {
            { "name", "Alice" },
            { "orderId", "ORD-9842" },
            { "trackingUrl", "https://track.example.com/9842" }
        });

        Console.WriteLine();
        service.SendNotification("user1", otpTemplate, new Dictionary<string, string>
        {
            { "name", "Alice" },
            { "otp", "482913" },
            { "expiry", "5" }
        });

        // ── Feature 2: Opt-Out ──
        Console.WriteLine("\n\n=== Feature 2: Per-User Opt-Out ===\n");

        // Bob prefers Email + SMS + Push, but opts out of SMS
        var bobPrefs = new UserPreference("user2",
            new HashSet<ChannelType> { ChannelType.Email, ChannelType.Sms, ChannelType.Push },
            optedOutChannels: new HashSet<ChannelType> { ChannelType.Sms });

        preferenceService.SavePreference(bobPrefs);

        Console.WriteLine("  Bob opted out of SMS. Active channels: " +
            string.Join(", ", bobPrefs.GetActiveChannels()));
        service.SendNotification("user2", orderTemplate, new Dictionary<string, string>
        {
            { "name", "Bob" },
            { "orderId", "ORD-1234" },
            { "trackingUrl", "https://track.example.com/1234" }
        });

        // Bob opts back into SMS
        Console.WriteLine("\n  Bob opts back into SMS:");
        bobPrefs.OptIn(ChannelType.Sms);
        Console.WriteLine("  Active channels: " + string.Join(", ", bobPrefs.GetActiveChannels()));
        service.SendNotification("user2", otpTemplate, new Dictionary<string, string>
        {
            { "name", "Bob" },
            { "otp", "991122" },
            { "expiry", "3" }
        });

        // Charlie opts out of everything
        Console.WriteLine("\n  Charlie opts out of ALL channels:");
        var charliePrefs = new UserPreference("user3",
            new HashSet<ChannelType> { ChannelType.Email, ChannelType.Push },
            optedOutChannels: new HashSet<ChannelType> { ChannelType.Email, ChannelType.Push });
        preferenceService.SavePreference(charliePrefs);

        service.SendNotification(new Notification("user3", "This should not be delivered"));
    }
}
