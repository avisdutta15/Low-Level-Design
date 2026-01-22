using System.Collections.Concurrent;

namespace DeepSeekRateLimiter
{
    // Rate limit algorithm types
    public enum RateLimitAlgorithm
    {
        LeakyBucket,
        TokenBucket,
        FixedWindow,
        SlidingWindow
    }

    // Configuration for rate limiter
    public record RateLimitConfig(
        RateLimitAlgorithm Algorithm,
        int Capacity,
        double RatePerSecond,
        TimeSpan? WindowSize = null
    );

    // Result of rate limit check
    public record RateLimitResult(
        bool IsAllowed,
        int Remaining,
        TimeSpan RetryAfter
    );

    // Key to identify rate limiters
    public record RateLimitKey(string Type, string Identifier)
    {
        public override string ToString() => $"{Type}:{Identifier}";

        // Common key factories
        public static RateLimitKey ForUser(string userId) => new("User", userId);
        public static RateLimitKey ForIp(string ipAddress) => new("IP", ipAddress);
        public static RateLimitKey ForService(string serviceName) => new("Service", serviceName);
    }

    // Main rate limiter interface
    public interface IRateLimiter
    {
        RateLimitAlgorithm Algorithm { get; }
        Task<RateLimitResult> TryAcquireAsync(int permits = 1);
        void Configure(RateLimitConfig config);
    }

    public class LeakyBucketRateLimiter : IRateLimiter
    {
        public RateLimitAlgorithm Algorithm => RateLimitAlgorithm.LeakyBucket;

        private readonly object _syncRoot = new();
        private double _currentLevel;
        private DateTime _lastLeakTime;
        private int _capacity;
        private double _leakRatePerMs;

        public LeakyBucketRateLimiter(RateLimitConfig config)
        {
            Configure(config);
        }

        public void Configure(RateLimitConfig config)
        {
            lock (_syncRoot)
            {
                _capacity = config.Capacity;
                _leakRatePerMs = config.RatePerSecond / 1000.0;
                _currentLevel = 0;
                _lastLeakTime = DateTime.UtcNow;
            }
        }

        public Task<RateLimitResult> TryAcquireAsync(int permits = 1)
        {
            if (permits <= 0) throw new ArgumentException("Permits must be positive");

            lock (_syncRoot)
            {
                Refill();

                if (_currentLevel + permits <= _capacity)
                {
                    _currentLevel += permits;
                    return Task.FromResult(new RateLimitResult(
                        IsAllowed: true,
                        Remaining: _capacity - (int)Math.Ceiling(_currentLevel),
                        RetryAfter: TimeSpan.Zero
                    ));
                }
                else
                {
                    var timeToNextLeak = GetTimeToNextLeak();
                    return Task.FromResult(new RateLimitResult(
                        IsAllowed: false,
                        Remaining: 0,
                        RetryAfter: timeToNextLeak
                    ));
                }
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var timeElapsed = now - _lastLeakTime;

            if (timeElapsed > TimeSpan.Zero)
            {
                var leakedAmount = timeElapsed.TotalMilliseconds * _leakRatePerMs;
                _currentLevel = Math.Max(0, _currentLevel - leakedAmount);
                _lastLeakTime = now;
            }
        }

        private TimeSpan GetTimeToNextLeak()
        {
            if (_currentLevel <= 0) return TimeSpan.Zero;

            var msUntilEmpty = _currentLevel / _leakRatePerMs;
            var nextLeakTime = _lastLeakTime.AddMilliseconds(msUntilEmpty);
            var waitTime = nextLeakTime - DateTime.UtcNow;

            return waitTime > TimeSpan.Zero ? waitTime : TimeSpan.Zero;
        }
    }

    public class TokenBucketRateLimiter : IRateLimiter
    {
        public RateLimitAlgorithm Algorithm => RateLimitAlgorithm.TokenBucket;

        public Task<RateLimitResult> TryAcquireAsync(int permits = 1)
        {
            // TODO: Implement token bucket algorithm
            throw new NotImplementedException("TokenBucket not implemented yet");
        }

        public void Configure(RateLimitConfig config)
        {
            // Configuration logic here
        }
    }

    public class FixedWindowRateLimiter : IRateLimiter
    {
        public RateLimitAlgorithm Algorithm => RateLimitAlgorithm.FixedWindow;

        public Task<RateLimitResult> TryAcquireAsync(int permits = 1)
        {
            // TODO: Implement fixed window algorithm
            throw new NotImplementedException("FixedWindow not implemented yet");
        }

        public void Configure(RateLimitConfig config)
        {
            // Configuration logic here
        }
    }

    public class SlidingWindowRateLimiter : IRateLimiter
    {
        public RateLimitAlgorithm Algorithm => RateLimitAlgorithm.SlidingWindow;

        public Task<RateLimitResult> TryAcquireAsync(int permits = 1)
        {
            // TODO: Implement sliding window algorithm
            throw new NotImplementedException("SlidingWindow not implemented yet");
        }

        public void Configure(RateLimitConfig config)
        {
            // Configuration logic here
        }
    }

    public static class RateLimiterFactory
    {
        public static IRateLimiter Create(RateLimitConfig config)
        {
            return config.Algorithm switch
            {
                RateLimitAlgorithm.LeakyBucket => new LeakyBucketRateLimiter(config),
                RateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(),
                RateLimitAlgorithm.FixedWindow => new FixedWindowRateLimiter(),
                RateLimitAlgorithm.SlidingWindow => new SlidingWindowRateLimiter(),
                _ => throw new ArgumentException($"Unknown algorithm: {config.Algorithm}")
            };
        }
    }

    public class RateLimiterService
    {
        private readonly ConcurrentDictionary<string, IRateLimiter> _limiters;

        public RateLimiterService()
        {
            _limiters = new ConcurrentDictionary<string, IRateLimiter>();
        }

        public void ConfigureRateLimit(RateLimitKey key, RateLimitConfig config)
        {
            var limiter = RateLimiterFactory.Create(config);
            _limiters[key.ToString()] = limiter;

            Console.WriteLine($"Configured {config.Algorithm} for {key}: {config.Capacity} capacity, {config.RatePerSecond}/sec");
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(RateLimitKey key, int permits = 1)
        {
            var keyString = key.ToString();

            if (!_limiters.TryGetValue(keyString, out var limiter))
            {
                // No rate limiting configured for this key
                return new RateLimitResult(true, int.MaxValue, TimeSpan.Zero);
            }

            return await limiter.TryAcquireAsync(permits);
        }

        public void RemoveRateLimit(RateLimitKey key)
        {
            _limiters.TryRemove(key.ToString(), out _);
        }

        public int GetActiveLimitersCount() => _limiters.Count;
    }

    public class Program
    {
        public static async Task Main()
        {
            Console.WriteLine("=== Simple Rate Limiter Service Demo ===\n");

            var rateLimiterService = new RateLimiterService();

            // Configure different rate limits
            rateLimiterService.ConfigureRateLimit(
                RateLimitKey.ForUser("alice"),
                new RateLimitConfig(RateLimitAlgorithm.LeakyBucket, Capacity: 5, RatePerSecond: 1.0)
            );

            rateLimiterService.ConfigureRateLimit(
                RateLimitKey.ForUser("bob"),
                new RateLimitConfig(RateLimitAlgorithm.LeakyBucket, Capacity: 10, RatePerSecond: 2.0)
            );

            rateLimiterService.ConfigureRateLimit(
                RateLimitKey.ForIp("192.168.1.1"),
                new RateLimitConfig(RateLimitAlgorithm.LeakyBucket, Capacity: 100, RatePerSecond: 10.0)
            );

            Console.WriteLine($"Active rate limiters: {rateLimiterService.GetActiveLimitersCount()}\n");

            // Test Alice's rate limit (strict)
            await TestUser(rateLimiterService, "alice", 8);

            // Test Bob's rate limit (more generous)
            await TestUser(rateLimiterService, "bob", 12);

            // Test unknown user (no rate limiting)
            await TestUser(rateLimiterService, "charlie", 5);

            Console.WriteLine("\n=== Demo Complete ===");
        }

        static async Task TestUser(RateLimiterService service, string userId, int requestCount)
        {
            Console.WriteLine($"\n--- Testing {userId} making {requestCount} requests ---");

            var key = RateLimitKey.ForUser(userId);
            var allowedCount = 0;
            var deniedCount = 0;

            for (int i = 1; i <= requestCount; i++)
            {
                var result = await service.CheckRateLimitAsync(key);

                if (result.IsAllowed)
                {
                    allowedCount++;
                    Console.WriteLine($"  Request {i}:  ALLOWED (Remaining: {result.Remaining})");
                }
                else
                {
                    deniedCount++;
                    Console.WriteLine($"  Request {i}:  DENIED (Retry after: {result.RetryAfter.TotalSeconds}s)");

                    // Simulate waiting when rate limited
                    if (result.RetryAfter > TimeSpan.Zero)
                    {
                        await Task.Delay(result.RetryAfter);
                        // Retry the same request after waiting
                        i--; // Decrement to retry this same request
                        deniedCount--;
                    }
                }

                await Task.Delay(200); // Small delay between requests
            }

            Console.WriteLine($"  Results: {allowedCount} allowed, {deniedCount} denied");
        }
    }
}
