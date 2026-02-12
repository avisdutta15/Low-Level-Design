using System.Collections.Concurrent;

namespace RateLimiterSimpleService;

// Strategy Pattern
public interface IRateLimiter
{
    bool TryAcquire();
}

/*
 Key Logic Changes Explained
    cyclesPassed Calculation: Instead of multiplying time, we divide elapsedTime / _refillIntervalMs. 
                              If your interval is 1000ms and 2500ms have passed, cyclesPassed is 2.

    Time Drift Prevention:
    1. If we set _lastRefillTime = DateTime.UtcNow, we lose the extra 500ms from the example above (2500ms elapsed vs 2000ms 
                                accounted for).
    2. By doing _lastRefillTime.AddMilliseconds(cyclesPassed * _refillIntervalMs), that "remainder" 500ms stays in the 
    calculation for the next call, ensuring your rate is mathematically precise over long periods.

    Locking: The lock (_lock) ensures that checking the count and decrementing it happens as a single atomic operation.
 */

public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly int _refillIntervalMs;
    private readonly int _tokensToRefillPerRefillCycle;
    private readonly int _bucketLimit;
    private readonly object _lock = new object();

    private DateTime _lastRefillTime;
    private int _tokens;

    public TokenBucketRateLimiter(int tokensToRefillPerRefillCycle, int refillIntervalMs, int bucketLimit)
    {
        _bucketLimit = bucketLimit;
        _lastRefillTime = DateTime.UtcNow;
        _refillIntervalMs = refillIntervalMs;
        _tokensToRefillPerRefillCycle = tokensToRefillPerRefillCycle;
        _tokens = bucketLimit;      // Start with a full bucket
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            Refill();

            //if the user has enough tokens to process the request
            if (_tokens > 0)
            {
                //process the request
                _tokens--;
                return true;
            }
            return false;
        }
    }

    public void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsedTime = (now - _lastRefillTime).TotalMilliseconds;
        if (elapsedTime >= _refillIntervalMs)
        {
            // 1. Calculate how many full refill cycles have passed
            // Casting to int automatically floors the value (e.g., 2.5 cycles becomes 2)
            int cyclesPassed = (int)(elapsedTime / _refillIntervalMs);

            // 2. Calculate tokens to add
            int tokensToAdd = (int)(cyclesPassed * _tokensToRefillPerRefillCycle);

            if (tokensToAdd > 0)
            {
                // 3. Add tokens, clamping to the bucket limit
                _tokens = Math.Min(_bucketLimit, _tokens + tokensToAdd);
                //_lastRefillTime = now;
                _lastRefillTime.AddMilliseconds(cyclesPassed * _refillIntervalMs);
            }
        }
    }
}

public class FixedWindowRateLimiter : IRateLimiter
{
    private readonly int _requestsAllowedPerWindow;
    private readonly int _windowSizeInMs;
    private readonly object _lock = new object();

    private int _requestsMade;
    private DateTime _windowStartTime;

    public FixedWindowRateLimiter(int requestsAllowedPerWindow, int windowSizeInMs)
    {
        _requestsAllowedPerWindow = requestsAllowedPerWindow;
        _windowSizeInMs = windowSizeInMs;

        _requestsMade = 0;
        _windowStartTime = DateTime.UtcNow;
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var elapsedTime = (now - _windowStartTime).TotalMilliseconds;

            //if elapsedTime is more than the window size then reset the start time and the count
            if (elapsedTime >= _windowSizeInMs)
            {
                _windowStartTime = now;
                _requestsMade = 0;
            }

            if (_requestsMade < _requestsAllowedPerWindow)
            {
                _requestsMade++;
                return true;
            }
            return false;
        }
    }
}

public class SlidingWindowLogRateLimiter : IRateLimiter
{
    private readonly int _requestsAllowedPerWindow;
    private readonly int _windowSizeInMs;
    private readonly object _lock = new object();

    private Queue<DateTime> _requestTimeStamps = new Queue<DateTime>();

    public SlidingWindowLogRateLimiter(int requestsAllowedPerWindow, int windowSizeInMs)
    {
        _requestsAllowedPerWindow = requestsAllowedPerWindow;
        _windowSizeInMs = windowSizeInMs;
    }
    public bool TryAcquire()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            while (_requestTimeStamps.Count > 0 && ((now - _requestTimeStamps.Peek()).TotalMilliseconds > _windowSizeInMs))
            {
                _requestTimeStamps.Dequeue();
            }

            if (_requestTimeStamps.Count < _requestsAllowedPerWindow)
            {
                _requestTimeStamps.Enqueue(now);
                return true;
            }
            return false;
        }
    }
}

public class LeakyBucketRateLimiter : IRateLimiter
{
    private readonly int _maxBucketCapacity;     // Size of the bucket (Burst limit)
    private readonly int _outflowTokens;           // e.g., 10 requests...
    private readonly int _leakIntervalMs;        // ...per 1000 ms
    private readonly object _lock = new();

    private double _currentWaterLevel;           // Current requests in queue
    private DateTime _lastLeakTime;              // Last time we calculated a leak

    public LeakyBucketRateLimiter(int outflowTokens, int leakIntervalMs, int bucketLimit)
    {
        _outflowTokens = outflowTokens;
        _leakIntervalMs = leakIntervalMs;
        _maxBucketCapacity = bucketLimit;

        _currentWaterLevel = 0;
        _lastLeakTime = DateTime.UtcNow;
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            Leak();

            // Check if adding 1 unit of water would overflow the bucket
            if ((_currentWaterLevel + 1) <= _maxBucketCapacity)
            {
                // Add the request (water) to the bucket
                _currentWaterLevel++;
                return true;
            }
            // Bucket is full (Overflow)
            return false;
        }
    }

    public void Leak()
    {
        // Step 1: Calculate how much time has passed
        var now = DateTime.UtcNow;
        var elapsedTime = (now - _lastLeakTime).TotalMilliseconds;

        if (elapsedTime <= 0)
            return;

        // Step 2: Calculate the "Leak Rate" per millisecond
        // We cast to double to ensure floating-point precision
        double leakRatePerMs = (double)_outflowTokens / _leakIntervalMs;

        // Step 3: Calculate total water to drain based on elapsed time
        double waterToDrain = elapsedTime * leakRatePerMs;

        // Step 4: Apply the leak (Drain the water)
        // We ensure the water level never drops below zero
        if (waterToDrain > 0)
        {
            _currentWaterLevel = _currentWaterLevel - waterToDrain;

            // Handle the "Empty Bucket" edge case
            if (_currentWaterLevel < 0)
            {
                _currentWaterLevel = 0;

            }

            // Step 5: Update the timestamp
            // Since we use 'double' for water level (continuous flow), 
            // we can safely set the last leak time to 'now'.
            _lastLeakTime = now;
        }
    }
}

public enum RateLimitAlgorithm
{
    TokenBucket,
    LeakyBucket,
    FixedWindow,
    SlidingWindow
}

/* Client Identity */
public enum ClientType
{
    User,
    Service,
    IP
}

public enum TierType
{
    Free,
    Premium,
    Internal
}

public class ClientIdentity
{
    public string Id {  get; set; }
    public ClientType ClientType { get; set; }
    public TierType Tier { get; set; }

    public override string ToString()
    {
        return $"{ClientType}:{Tier}";
    }
}

/* Rules Configuration and builder */
public class RateLimitRule
{
    public RateLimitAlgorithm RateLimitAlgorithm { get; set; }
    public int TokensToRefillPerRefillCycle { get; set; } 
    public int RefillIntervalMs { get; set; }
    public int BucketLimit { get; set; }
    public int RequestsAllowedPerWindow { get; set; }
    public int WindowSizeInMs { get; set; }
    public int outflowTokens { get; set; }
    public int leakIntervalMs { get; set; }
}

public class RuleStore
{
    //We can get the rules from Db or some other data-stores
    public RateLimitRule? GetRule(ClientIdentity client)
    {
        // Example logic: Different rules for different Tiers and Types
        if (client.ClientType == ClientType.IP)
            return new RateLimitRule { RateLimitAlgorithm = RateLimitAlgorithm.FixedWindow, RequestsAllowedPerWindow = 10, WindowSizeInMs = 1000 };

        return client.Tier switch
        {
            TierType.Premium => new RateLimitRule { RateLimitAlgorithm = RateLimitAlgorithm.TokenBucket, BucketLimit = 1000, TokensToRefillPerRefillCycle = 10, RefillIntervalMs = 100 },
            TierType.Internal => new RateLimitRule { RateLimitAlgorithm = RateLimitAlgorithm.TokenBucket, BucketLimit = 10000, TokensToRefillPerRefillCycle = 100, RefillIntervalMs = 100 },
            _ => new RateLimitRule { RateLimitAlgorithm = RateLimitAlgorithm.TokenBucket, BucketLimit = 5, TokensToRefillPerRefillCycle = 1, RefillIntervalMs = 1000 } // Free tier
        };
    }
}

/* Create the RateLimiter Factory */
public static class RateLimiterFactory
{
    //Deprecated: This was hardcoded.
    public static IRateLimiter Create(RateLimitAlgorithm rateLimitAlgorithm)
    {
        return rateLimitAlgorithm switch
        {
            RateLimitAlgorithm.FixedWindow => new FixedWindowRateLimiter(requestsAllowedPerWindow: 10, windowSizeInMs: 1),
            RateLimitAlgorithm.SlidingWindow => new SlidingWindowLogRateLimiter(requestsAllowedPerWindow: 10, windowSizeInMs: 3),
            RateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(tokensToRefillPerRefillCycle: 5, refillIntervalMs: 1000, bucketLimit: 10),
            RateLimitAlgorithm.LeakyBucket => new LeakyBucketRateLimiter(outflowTokens: 5, leakIntervalMs: 1000, bucketLimit: 10),
            _ => throw new NotImplementedException()
        };
    }

    //Overload accepting a rule
    public static IRateLimiter Create(RateLimitRule rule)
    {
        return rule.RateLimitAlgorithm switch
        {
            RateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(tokensToRefillPerRefillCycle: rule.TokensToRefillPerRefillCycle, refillIntervalMs: rule.RefillIntervalMs, bucketLimit: rule.BucketLimit),
            RateLimitAlgorithm.LeakyBucket => new LeakyBucketRateLimiter(outflowTokens: rule.outflowTokens, leakIntervalMs: rule.leakIntervalMs, bucketLimit: rule.BucketLimit),
            RateLimitAlgorithm.FixedWindow => new FixedWindowRateLimiter(requestsAllowedPerWindow: rule.RequestsAllowedPerWindow, windowSizeInMs: rule.WindowSizeInMs),
            RateLimitAlgorithm.SlidingWindow => new SlidingWindowLogRateLimiter(requestsAllowedPerWindow: rule.RequestsAllowedPerWindow, windowSizeInMs: rule.WindowSizeInMs),
            _ => throw new NotImplementedException()
        };
    }
}

public class RateLimiterService
{
    private ConcurrentDictionary<string, IRateLimiter> _cache;
    private readonly RuleStore _ruleStore;
    public RateLimiterService(RuleStore ruleStore)
    {
        _cache = new();
        _ruleStore = ruleStore;
    }

    //Deprecated: We no longer configure directly. We use the rules store to configure
    public void Configure(string userId, RateLimitAlgorithm rateLimitAlgorithm)
    {
        var rateLimiter = RateLimiterFactory.Create(rateLimitAlgorithm);
        _cache.AddOrUpdate(userId, rateLimiter, (key, existingLimiter) => rateLimiter);
        Console.WriteLine($"[Config] User '{userId}' configured with {rateLimitAlgorithm}");
    }

    //Deprecated: We now pass a ClientIdentity
    public bool IsAllowed(string userId)
    {
        if (_cache.TryGetValue(userId, out var rateLimiter))
        {
            return rateLimiter.TryAcquire();
        }
        Console.WriteLine($"[Warning] User {userId} has no rate limit configured. Denying.");
        return false;
    }

    //Overload
    public bool IsAllowed(ClientIdentity client)
    {
        string key = client.ToString();

        IRateLimiter rateLimiter = _cache.GetOrAdd(key, _ =>
        {
            // This lambda only runs if the key doesn't exist
            var rule = _ruleStore.GetRule(client);
            return RateLimiterFactory.Create(rule);
        });

        return rateLimiter.TryAcquire();
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var ruleStore = new RuleStore();
        var service = new RateLimiterService(ruleStore);

        // Scenario 1: Free User (Token Bucket, 5 reqs)
        var freeUser = new ClientIdentity { Id = "user_123", ClientType = ClientType.User, Tier = TierType.Free };
        Console.WriteLine($"Free User: {service.IsAllowed(freeUser)}"); // True

        // Scenario 2: Premium User (Token Bucket, 1000 reqs)
        var premiumUser = new ClientIdentity { Id = "user_999", ClientType = ClientType.User, Tier = TierType.Premium };
        Console.WriteLine($"Premium User: {service.IsAllowed(premiumUser)}"); // True

        // Scenario 3: IP Address (Fixed Window, 10 reqs)
        var ipClient = new ClientIdentity { Id = "192.168.1.1", ClientType = ClientType.IP };
        Console.WriteLine($"IP Address: {service.IsAllowed(ipClient)}"); // True
    }
}