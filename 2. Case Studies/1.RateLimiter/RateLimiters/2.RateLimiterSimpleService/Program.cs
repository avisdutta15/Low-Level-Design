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

/* Create the RateLimiter Factory */

public enum RateLimitAlgorithm
{
    TokenBucket,
    LeakyBucket,
    FixedWindow,
    SlidingWindow
}

public static class RateLimiterFactory
{
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
}

public class RateLimiterService
{
    private ConcurrentDictionary<string, IRateLimiter> _cache;
    public RateLimiterService()
    {
        _cache = new();
    }

    public void Configure(string userId, RateLimitAlgorithm rateLimitAlgorithm)
    {
        var rateLimiter = RateLimiterFactory.Create(rateLimitAlgorithm);
        _cache.AddOrUpdate(userId, rateLimiter, (key, existingLimiter) => rateLimiter);
        Console.WriteLine($"[Config] User '{userId}' configured with {rateLimitAlgorithm}");
    }

    public bool IsAllowed(string userId)
    {
        if (_cache.TryGetValue(userId, out var rateLimiter))
        {
            return rateLimiter.TryAcquire();
        }
        Console.WriteLine($"[Warning] User {userId} has no rate limit configured. Denying.");
        return false;
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var rateLimitService = new RateLimiterService();

        // Alice gets a Token Bucket (Good for bursts)
        rateLimitService.Configure("user_alice", RateLimitAlgorithm.TokenBucket);

        // Bob gets a Fixed Window (Strict counting)
        rateLimitService.Configure("user_bob", RateLimitAlgorithm.FixedWindow);

        // 3. Simulate Traffic
        Console.WriteLine("\n--- Simulating Traffic for Alice ---");
        for (int i = 0; i < 7; i++)
        {
            bool allowed = rateLimitService.IsAllowed("user_alice");
            Console.WriteLine($"Alice Request {i + 1}: {(allowed ? "Allowed" : "Denied")}");
        }

        Console.WriteLine("\n--- Simulating Traffic for Bob ---");
        for (int i = 0; i < 7; i++)
        {
            bool allowed = rateLimitService.IsAllowed("user_bob");
            Console.WriteLine($"Bob Request {i + 1}: {(allowed ? "Allowed" : "Denied")}");
        }

        // 4. Test Unconfigured User
        Console.WriteLine("\n--- Unconfigured User ---");
        rateLimitService.IsAllowed("user_charlie"); // Should print warning and deny
    }
}