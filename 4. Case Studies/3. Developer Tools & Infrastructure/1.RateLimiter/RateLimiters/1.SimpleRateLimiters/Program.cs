namespace SimpleRateLimiters;


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

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== TokenBucket Rate Limiter Tests ===\n");
        TokenBucketRateLimiterTests.Test1_Basic_Token_Consumption();
        TokenBucketRateLimiterTests.Test2_Token_Refill_After_Waiting();
        TokenBucketRateLimiterTests.Test3_Multiple_RefillCycles();
        TokenBucketRateLimiterTests.Test4_Bucket_Limit_Enforcement();
        Console.WriteLine("\n=== Tests Complete ===");
    }
}

public class TokenBucketRateLimiterTests
{
    public static void Test1_Basic_Token_Consumption()
    {
        // Test 1: Basic token consumption
        Console.WriteLine("Test 1: Basic token consumption: tokensToRefillPerRefillCycle: 5, refillIntervalMs: 1000, bucketLimit: 10");
        var limiter1 = new TokenBucketRateLimiter(tokensToRefillPerRefillCycle: 5, refillIntervalMs: 1000, bucketLimit: 10);
        for (int i = 0; i < 12; i++)
        {
            bool allowed = limiter1.TryAcquire();
            Console.WriteLine($"Request {i + 1}: {(allowed ? "ALLOWED" : "DENIED")}");
        }
    }

    public static void Test2_Token_Refill_After_Waiting()
    {
        // Test 2: Token refill after waiting
        Console.WriteLine("\nTest 2: Token refill after waiting: tokensToRefillPerRefillCycle: 3, refillIntervalMs: 1000, bucketLimit: 5");
        var limiter2 = new TokenBucketRateLimiter(tokensToRefillPerRefillCycle: 3, refillIntervalMs: 1000, bucketLimit: 5);
        Console.WriteLine("Consuming all tokens...");
        for (int i = 0; i < 5; i++)
        {
            limiter2.TryAcquire();
        }
        Console.WriteLine($"Next request: {(limiter2.TryAcquire() ? "ALLOWED" : "DENIED")} (should be DENIED)");

        Console.WriteLine("Waiting 1 second for refill...");
        Thread.Sleep(1000);
        Console.WriteLine($"After 1s: {(limiter2.TryAcquire() ? "ALLOWED" : "DENIED")} (should be ALLOWED - 3 tokens refilled)");
        Console.WriteLine($"After 1s: {(limiter2.TryAcquire() ? "ALLOWED" : "DENIED")} (should be ALLOWED)");
        Console.WriteLine($"After 1s: {(limiter2.TryAcquire() ? "ALLOWED" : "DENIED")} (should be ALLOWED)");
        Console.WriteLine($"After 1s: {(limiter2.TryAcquire() ? "ALLOWED" : "DENIED")} (should be DENIED - all 3 consumed)");
    }

    public static void Test3_Multiple_RefillCycles()
    {
        // Test 3: Multiple refill cycles
        Console.WriteLine("\nTest 3: Multiple refill cycles: tokensToRefillPerRefillCycle: 2, refillIntervalMs: 500, bucketLimit: 10");
        var limiter3 = new TokenBucketRateLimiter(tokensToRefillPerRefillCycle: 2, refillIntervalMs: 500, bucketLimit: 10);
        limiter3.TryAcquire(); // Consume 1 token (9 left)
        Console.WriteLine("Waiting 1.5 seconds (3 refill cycles)...");
        Thread.Sleep(1500);
        Console.WriteLine($"After 1.5s: Should have refilled 6 tokens (2 * 3 cycles), capped at bucket limit");
        for (int i = 0; i < 11; i++)
        {
            bool allowed = limiter3.TryAcquire();
            Console.WriteLine($"Request {i + 1}: {(allowed ? "ALLOWED" : "DENIED")}");
        }
    }

    public static void Test4_Bucket_Limit_Enforcement()
    {
        // Test 4: Bucket limit enforcement
        Console.WriteLine("\nTest 4: Bucket limit enforcement: tokensToRefillPerRefillCycle: 10, refillIntervalMs: 1000, bucketLimit: 5");
        var limiter4 = new TokenBucketRateLimiter(tokensToRefillPerRefillCycle: 10, refillIntervalMs: 1000, bucketLimit: 5);
        Console.WriteLine("Waiting 2 seconds (should refill 20 tokens, but capped at 5)...");
        Thread.Sleep(2000);
        for (int i = 0; i < 7; i++)
        {
            bool allowed = limiter4.TryAcquire();
            Console.WriteLine($"Request {i + 1}: {(allowed ? "ALLOWED" : "DENIED")}");
        }
    }
}