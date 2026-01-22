namespace RateLimiter
{
    public interface IRateLimiter
    {
        ValueTask<bool> TryAcquireAsync(int permits = 1, CancellationToken cancellationToken = default);
        RateLimitInfo GetRateLimitInfo();
        void Dispose();
    }
    public record RateLimitInfo(
        int Capacity,
        int CurrentLevel,
        double LeakRatePerSecond,
        TimeSpan TimeToNextLeak,
        bool IsAllowed
    );
    public class RateLimitExceededException : Exception
    {
        public RateLimitInfo RateLimitInfo { get; }

        public RateLimitExceededException(RateLimitInfo rateLimitInfo)
            : base($"Rate limit exceeded. Current level: {rateLimitInfo.CurrentLevel}/{rateLimitInfo.Capacity}")
        {
            RateLimitInfo = rateLimitInfo;
        }
    }
    public class LeakyBucketOptions
    {
        public int Capacity { get; set; } = 100;
        public double LeakRatePerSecond { get; set; } = 10.0;
        public int InitialLevel { get; set; } = 0;
        public bool AutoRefill { get; set; } = true;
    }
    public class LeakyBucketRateLimiter : IRateLimiter
    {
        private readonly int _capacity;
        private readonly double _leakRatePerSecond; // Items per second
        private readonly double _leakRatePerMillisecond;
        private readonly object _syncRoot = new object();
        private readonly bool _autoRefill;

        private double _currentLevel;
        private DateTime _lastLeakTime;
        private bool _disposed;

        public LeakyBucketRateLimiter(LeakyBucketOptions options)
        {
            if (options.Capacity <= 0)
                throw new ArgumentException("Capacity must be greater than 0", nameof(options));
            if (options.LeakRatePerSecond <= 0)
                throw new ArgumentException("Leak rate must be greater than 0", nameof(options));

            _capacity = options.Capacity;
            _leakRatePerSecond = options.LeakRatePerSecond;
            _leakRatePerMillisecond = _leakRatePerSecond / 1000.0;
            _autoRefill = options.AutoRefill;
            _currentLevel = options.InitialLevel;
            _lastLeakTime = DateTime.UtcNow;

            //LogState("Initialized");
        }

        public LeakyBucketRateLimiter(int capacity, double leakRatePerSecond, bool autoRefill = true)
            : this(new LeakyBucketOptions
            {
                Capacity = capacity,
                LeakRatePerSecond = leakRatePerSecond,
                AutoRefill = autoRefill
            })
        {
        }

        public ValueTask<bool> TryAcquireAsync(int permits = 1, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LeakyBucketRateLimiter));

            if (permits <= 0)
                throw new ArgumentException("Permits must be greater than 0", nameof(permits));

            var result = TryAcquireInternal(permits);
            return new ValueTask<bool>(result);
        }

        public void Acquire(int permits = 1)
        {
            if (!TryAcquireInternal(permits))
            {
                var info = GetRateLimitInfo();
                throw new RateLimitExceededException(info);
            }
        }

        public async ValueTask AcquireAsync(int permits = 1, CancellationToken cancellationToken = default)
        {
            while (!TryAcquireInternal(permits))
            {
                var waitTime = GetTimeToNextLeak();
                if (waitTime > TimeSpan.Zero)
                {
                    Console.WriteLine($"Rate limit exceeded. Waiting for {waitTime} until next leak");
                    await Task.Delay(waitTime, cancellationToken);
                }
            }
        }

        private bool TryAcquireInternal(int permits)
        {
            lock (_syncRoot)
            {
                var requestTime = DateTime.Now;

                if (_autoRefill)
                {
                    Refill();
                }

                bool allowed = _currentLevel + permits <= _capacity;

                if (allowed)
                {
                    _currentLevel += permits;
                    Console.WriteLine($"Request ALLOWED at {requestTime.ToString("HH:mm:ss.fff")}. Level: {_currentLevel}/{_capacity}, Permits: {permits}");
                }
                else
                {
                    Console.WriteLine($"Request DENIED at {requestTime.ToString("HH:mm:ss.fff")}. Level: {_currentLevel}/{_capacity}, Permits: {permits}");
                }

                //LogState($"After request ({(allowed ? "ALLOWED" : "DENIED")})");
                return allowed;
            }
        }

        public RateLimitInfo GetRateLimitInfo()
        {
            lock (_syncRoot)
            {
                Refill();
                var timeToNextLeak = GetTimeToNextLeak();

                return new RateLimitInfo(
                    Capacity: _capacity,
                    CurrentLevel: (int)Math.Ceiling(_currentLevel),
                    LeakRatePerSecond: _leakRatePerSecond,
                    TimeToNextLeak: timeToNextLeak,
                    IsAllowed: _currentLevel < _capacity
                );
            }
        }

        public void Refill()
        {
            if (_disposed) return;

            var now = DateTime.Now;
            var timeElapsed = now - _lastLeakTime;

            if (timeElapsed > TimeSpan.Zero)
            {
                var leakedAmount = timeElapsed.TotalMilliseconds * _leakRatePerMillisecond;
                var previousLevel = _currentLevel;
                _currentLevel = Math.Max(0, _currentLevel - leakedAmount);
                _lastLeakTime = now;

                if (leakedAmount > 0)
                {
                    Console.WriteLine($"Refill: Leaked {leakedAmount} units. Level: {previousLevel} -> {_currentLevel}." +
                        $"Time elapsed: {timeElapsed.TotalMilliseconds}ms, Last leak time: {_lastLeakTime.ToString("HH:mm:ss.fff")}");
                }
            }
        }

        private TimeSpan GetTimeToNextLeak()
        {
            if (_currentLevel <= 0)
                return TimeSpan.Zero;

            var millisecondsUntilEmpty = _currentLevel / _leakRatePerMillisecond;
            var nextLeakTime = _lastLeakTime.AddMilliseconds(millisecondsUntilEmpty);
            var now = DateTime.Now;

            var waitTime = nextLeakTime - now;
            return waitTime > TimeSpan.Zero ? waitTime : TimeSpan.Zero;
        }

        private void LogState(string operation)
        {
            Console.WriteLine(
                    "Operation: {operation} - LastLeakTime: {_lastLeakTime}, LastLeakTime: {_lastLeakTime.ToString(\"HH:mm:ss.fff\")}, " +
                    "Capacity: {_capacity}, LeakRate: {_leakRatePerSecond}/sec, TimeToNextLeak: {GetTimeToNextLeak()}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Console.WriteLine("LeakyBucketRateLimiter disposed");
                _disposed = true;
            }
        }

        // Method to get current state for debugging
        public (double CurrentLevel, DateTime LastLeakTime, TimeSpan TimeToNextLeak) GetCurrentState()
        {
            lock (_syncRoot)
            {
                return (_currentLevel, _lastLeakTime, GetTimeToNextLeak());
            }
        }
    }
    public static class RateLimiterFactory
    {
        public static IRateLimiter CreateLeakyBucket(int capacity, double leakRatePerSecond)
        {
            return new LeakyBucketRateLimiter(capacity, leakRatePerSecond, true);
        }

        public static IRateLimiter CreateLeakyBucket(LeakyBucketOptions options)
        {
            return new LeakyBucketRateLimiter(options);
        }
    }
    public class Program
    {
        public static async Task Main_1()
        {
            // Create rate limiter with logging
            var rateLimiter = new LeakyBucketRateLimiter(
                capacity: 5,
                leakRatePerSecond: 1.0 // 1 requests per second sustained
            );

            Console.WriteLine("=== Testing Leaky Bucket Rate Limiter. Config:  Capacity: 5, LeakRate: 1 ===");
            for (int i = 1; i <= 8; i++)
            {
                Console.WriteLine($"\n--- Request {i} ---");

                var allowed = await rateLimiter.TryAcquireAsync();

                if (allowed)
                {
                    Console.WriteLine($"Request {i} ALLOWED");
                }
                else
                {
                    Console.WriteLine($"Request {i} DENIED");

                    /*
                    // Check when we can make the next request
                    var state = rateLimiter.GetCurrentState();
                    Console.WriteLine($"   Current Level: {state.CurrentLevel:F2}/5");
                    Console.WriteLine($"   Time until next leak: {state.TimeToNextLeak.TotalSeconds:F2} seconds");

                    // Wait for enough capacity
                    if (state.TimeToNextLeak > TimeSpan.Zero)
                    {
                        Console.WriteLine($"   Waiting {state.TimeToNextLeak.TotalSeconds:F2} seconds...");
                        await Task.Delay(state.TimeToNextLeak);

                        // Retry the failed request after waiting
                        allowed = await rateLimiter.TryAcquireAsync();
                        if (allowed)
                        {
                            Console.WriteLine($"Request {i} ALLOWED after waiting");
                        }
                    }
                    */
                }

                // Small delay between requests to simulate processing time
                // await Task.Delay(100);
            }
            Console.WriteLine("\n=== Burst Test Complete ===");

            Console.WriteLine("=== Testing Leaky Bucket Rate Limiter. Config:  Capacity: 50, LeakRate: 5 ===");
            var factoryLimiter = RateLimiterFactory.CreateLeakyBucket(50, 5.0);
            for (int i = 1; i <= 8; i++)
            {
                Console.WriteLine($"\n--- Request {i} ---");
                await Task.Delay(1000);
                var allowed = await factoryLimiter.TryAcquireAsync();

                if (allowed)
                {
                    Console.WriteLine($"Request {i} ALLOWED");
                }
                else
                {
                    Console.WriteLine($"Request {i} DENIED");
                }
            }
            Console.WriteLine("\n=== Burst Test Complete ===");

            rateLimiter.Dispose();
        }
    }
}
