using System.Collections.Concurrent;

// Notification System V2
//
// Same as V1 but RetryPolicy is now a Strategy Pattern.
// Different channels or dispatchers can use different retry strategies:
//   - SimpleRetryPolicy: retry N times with no delay
//   - ExponentialBackoffRetryPolicy: retry with exponential delay between attempts
//   - NoRetryPolicy: fail immediately, no retries
//
// This allows:
//   - Email uses ExponentialBackoff (remote server, transient failures)
//   - SMS uses SimpleRetry (fast, just try again)
//   - Push uses NoRetry (real-time, stale pushes are useless)
//
// Design:
//   IRetryPolicy (interface)
//      - Execute(Func<bool> action) → bool
//   Dispatcher takes a Dictionary<ChannelType, IRetryPolicy> mapping
//   Each channel gets its own retry strategy

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum ChannelType
{
    Email,
    Sms,
    Push
}

public enum NotificationType
{
    Alert,
    Reminder,
    PaymentUpdate,
    Message
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
// Retry Policy (Strategy Pattern)
// ─────────────────────────────────────────────
public interface IRetryPolicy
{
    // Executes the action with retry logic. Returns true if action eventually succeeded.
    bool Execute(Func<bool> action, string context);
}

// No retry — try once, done.
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

// Simple retry — try up to maxRetries times, no delay.
public class SimpleRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;

    public SimpleRetryPolicy(int maxRetries = 3)
    {
        _maxRetries = maxRetries;
    }

    public bool Execute(Func<bool> action, string context)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            bool success = action();
            if (success) return true;

            if (attempt < _maxRetries)
                Console.WriteLine($"    [{context}] Retrying... ({attempt}/{_maxRetries})");
        }
        Console.WriteLine($"    [{context}] FAILED after {_maxRetries} retries.");
        return false;
    }
}

// Exponential backoff — retry with increasing delays (50ms, 100ms, 200ms, ...)
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
            bool success = action();
            if (success) return true;

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

// Simulates a channel that fails the first N-1 attempts
public class FlakyChannel : INotificationChannel
{
    private readonly string _name;
    private int _attempts;
    private readonly int _failUntilAttempt;

    public FlakyChannel(string name, int failUntilAttempt = 3)
    {
        _name = name;
        _failUntilAttempt = failUntilAttempt;
    }

    public bool Send(Notification notification)
    {
        _attempts++;
        if (_attempts < _failUntilAttempt)
        {
            Console.WriteLine($"    [{_name}] FAILED attempt {_attempts}");
            return false;
        }
        Console.WriteLine($"    [{_name}] Sent to {notification.UserId}: {notification.Message} (attempt {_attempts})");
        _attempts = 0;
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
// NotificationDispatcher (uses retry policy per channel)
// ─────────────────────────────────────────────
public class NotificationDispatcher
{
    private readonly UserPreferenceService _preferenceService;
    private readonly Dictionary<ChannelType, IRetryPolicy> _retryPolicies;
    private readonly Dictionary<ChannelType, INotificationChannel>? _channelOverride;
    private readonly IRetryPolicy _defaultPolicy;

    public NotificationDispatcher(
        UserPreferenceService preferenceService,
        Dictionary<ChannelType, IRetryPolicy> retryPolicies,
        Dictionary<ChannelType, INotificationChannel>? channelOverride = null)
    {
        _preferenceService = preferenceService;
        _retryPolicies = retryPolicies;
        _defaultPolicy = new SimpleRetryPolicy(3);
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

            // Get the retry policy for this channel type (or default)
            IRetryPolicy policy = _retryPolicies.ContainsKey(channelType)
                ? _retryPolicies[channelType]
                : _defaultPolicy;

            policy.Execute(() => channel.Send(notification), channelType.ToString());
        }
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
    public static void Main(string[] args)
    {
        UserPreferenceService preferenceService = new UserPreferenceService();

        preferenceService.SavePreference(
            new UserPreference("user123", new HashSet<ChannelType> { ChannelType.Email, ChannelType.Sms, ChannelType.Push }));

        // Different retry policy per channel:
        //   Email → Exponential Backoff (transient server failures)
        //   SMS   → Simple Retry 3x (fast network, just try again)
        //   Push  → No Retry (real-time, stale pushes are useless)
        var retryPolicies = new Dictionary<ChannelType, IRetryPolicy>
        {
            { ChannelType.Email, new ExponentialBackoffRetryPolicy(maxRetries: 4, baseDelayMs: 50) },
            { ChannelType.Sms, new SimpleRetryPolicy(maxRetries: 3) },
            { ChannelType.Push, new NoRetryPolicy() }
        };

        // ── Demo 1: All channels succeed (retry not triggered) ──
        Console.WriteLine("=== All channels succeed ===");
        var dispatcher = new NotificationDispatcher(preferenceService, retryPolicies);
        var service = new NotificationService(dispatcher);
        service.SendNotification(new Notification("user123", "Your order shipped!"));

        // ── Demo 2: Flaky Email with Exponential Backoff ──
        Console.WriteLine("\n=== Flaky Email (Exponential Backoff) ===");
        var flakyEmailDispatcher = new NotificationDispatcher(preferenceService, retryPolicies,
            channelOverride: new Dictionary<ChannelType, INotificationChannel>
            {
                { ChannelType.Email, new FlakyChannel("Email", failUntilAttempt: 3) }
            });
        new NotificationService(flakyEmailDispatcher)
            .SendNotification(new Notification("user123", "Payment received"));

        // ── Demo 3: Flaky SMS with Simple Retry ──
        Console.WriteLine("\n=== Flaky SMS (Simple Retry) ===");
        var flakySmsDispatcher = new NotificationDispatcher(preferenceService, retryPolicies,
            channelOverride: new Dictionary<ChannelType, INotificationChannel>
            {
                { ChannelType.Sms, new FlakyChannel("SMS", failUntilAttempt: 2) }
            });
        new NotificationService(flakySmsDispatcher)
            .SendNotification(new Notification("user123", "OTP: 482913"));

        // ── Demo 4: Flaky Push with No Retry (fails immediately) ──
        Console.WriteLine("\n=== Flaky Push (No Retry — drops immediately) ===");
        var flakyPushDispatcher = new NotificationDispatcher(preferenceService, retryPolicies,
            channelOverride: new Dictionary<ChannelType, INotificationChannel>
            {
                { ChannelType.Push, new FlakyChannel("Push", failUntilAttempt: 3) }
            });
        new NotificationService(flakyPushDispatcher)
            .SendNotification(new Notification("user123", "New message from Alice"));
    }
}
