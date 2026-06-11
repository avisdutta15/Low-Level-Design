using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace _3.RateLimiterConfigurationAndService
{
    public enum RateLimitAlgorithm
    {
        TokenBucket,
        LeakyBycket,
        FixedWindow,
        SlidingWindow
    }

    // The base for all configurations
    public abstract class RateLimitAlgorithmConfig { }
    public class TokenBucketConfig : RateLimitAlgorithmConfig
    { 
        public int TokensToRefillPerRefillCycle { get; set; }
        public int RefillIntervalInSec { get; set; }
        public int Capacity { get; set; }
    }

    public class FixedWindowConfig : RateLimitAlgorithmConfig
    {
        public int RequestsAllowedPerWindow { get; set; }
        public int WindowSizeInSeconds { get; set; }
    }

    public class EndpointConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public RateLimitAlgorithm Algorithm { get; set; }
        public RateLimitAlgorithmConfig? AlgoConfig { get; set; }
    }

    public record RateLimitResult(bool Allowed, int Remaining, TimeSpan? RetryAfter);

    public interface IRateLimiter
    {
        RateLimitResult TryAcquire();
    }

    public class TokenBucketRateLimiter : IRateLimiter
    {
        private readonly int _tokensToRefillPerRefillCycle;
        private readonly int _refillIntervalInSeconds;
        private readonly int _capacity;
        private readonly object _lock = new object();

        private DateTime _lastRefillTime;
        private int _tokens;
        public TokenBucketRateLimiter(int tokensToRefillPerRefillCycle, int refillIntervalInSeconds, int capacity)
        {
            _tokensToRefillPerRefillCycle = tokensToRefillPerRefillCycle;
            _refillIntervalInSeconds = refillIntervalInSeconds;
            _capacity = capacity;
            _lastRefillTime = DateTime.UtcNow;
            _tokens = capacity;     //start with a full bucket
        }

        public RateLimitResult TryAcquire()
        {
            lock (_lock)
            {
                RefillTokens();

                //1. if the user has enough tokens to process the request
                if (_tokens > 0)
                {
                    //process the request
                    _tokens--;
                    return new RateLimitResult(Allowed: true, Remaining: _tokens, RetryAfter: TimeSpan.Zero);
                }

                //2. Calculation: Time until the next refill cycle is complete
                var now = DateTime.UtcNow;
                var timeSpentInCurrentCycle = now - _lastRefillTime;
                var timeToNextRefill = TimeSpan.FromSeconds(_refillIntervalInSeconds) - timeSpentInCurrentCycle;

                //3. Rejection
                if (timeToNextRefill > TimeSpan.Zero)
                {
                    return new RateLimitResult(Allowed: false, 0, RetryAfter: timeToNextRefill);
                }
                else
                {
                    return new RateLimitResult(Allowed: false, 0, RetryAfter: TimeSpan.FromSeconds(_refillIntervalInSeconds));
                }
            }
        }

        private void RefillTokens()
        {
            var now = DateTime.UtcNow;
            var elapsedTime = (now - _lastRefillTime).TotalSeconds ;

            if(elapsedTime > _refillIntervalInSeconds)
            {
                //count the number of refill cycles that have passed.
                int cyclesElapsed = (int)elapsedTime/_refillIntervalInSeconds;

                var tokensToRefill = cyclesElapsed * _tokensToRefillPerRefillCycle;
                
                _tokens = Math.Min(tokensToRefill + _tokens, _capacity);
                _lastRefillTime = now;
            }
        }
    }

    public class FixedWindowRateLimiter : IRateLimiter
    {
        private readonly object _lock = new object();
        private readonly int _requestsAllowedPerWindow;
        private readonly int _windowSizeInSeconds;

        private DateTime _windowStartTime;
        private int _requestsMade;

        public FixedWindowRateLimiter(int requestsAllowedPerWindow, int windowSizeInSeconds) 
        {
            _requestsAllowedPerWindow = requestsAllowedPerWindow;
            _windowSizeInSeconds = windowSizeInSeconds;

            _windowStartTime = DateTime.UtcNow;
            _requestsMade = 0;
        }

        public RateLimitResult TryAcquire()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var elapsedTime = (now - _windowStartTime).TotalSeconds;

                //1. if elapsedTime is more than the window size then reset the start time and the count
                if (elapsedTime >= _windowSizeInSeconds)
                {
                    _windowStartTime = now;
                    _requestsMade = 0;
                }

                // 2. Admission Logic
                if (_requestsMade < _requestsAllowedPerWindow)
                {
                    _requestsMade++;
                    return new RateLimitResult(Allowed:true, Remaining:(_requestsAllowedPerWindow-_requestsMade), RetryAfter: TimeSpan.Zero);
                }

                // 3. Rejection Logic
                // Calculate exactly how long until the current window expires
                elapsedTime = (now - _windowStartTime).TotalMilliseconds;
                return new RateLimitResult(Allowed: false, Remaining: 0, RetryAfter: TimeSpan.FromSeconds(_windowSizeInSeconds - elapsedTime));               
            }
        }
    }

    
    public class SlidingWindowRateLimiter : IRateLimiter
    {
        private readonly int _requestsAllowedPerWindow;
        private readonly int _windowSizeInSeconds;
        private readonly object _lock = new object(); // Protect the queue

        private Queue<DateTime> _requestTimeStampsQ; 

        public SlidingWindowRateLimiter(int requestsAllowedPerWindow, int windowSizeInSeconds)
        {
            _requestsAllowedPerWindow = requestsAllowedPerWindow;
            _windowSizeInSeconds = windowSizeInSeconds;
            _requestTimeStampsQ = new Queue<DateTime>();
        }

        public RateLimitResult TryAcquire()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                while (_requestTimeStampsQ.Count > 0 && (now - _requestTimeStampsQ.Peek()).TotalSeconds > _windowSizeInSeconds)
                {
                    _requestTimeStampsQ.Dequeue();
                }

                if (_requestTimeStampsQ.Count < _requestsAllowedPerWindow)
                {
                    _requestTimeStampsQ.Enqueue(now);
                    return new RateLimitResult(Allowed: true, Remaining: (_requestsAllowedPerWindow - _requestTimeStampsQ.Count), RetryAfter: TimeSpan.Zero);
                }

                // 3. Rejection Logic: Calculate precise wait time
                // The next slot opens exactly when the oldest request in the queue "falls out" of the window
                var oldestRequestTime = _requestTimeStampsQ.Peek();
                var timeSinceOldest = now - oldestRequestTime;
                var timeUntilExpiry = TimeSpan.FromSeconds(_windowSizeInSeconds) - timeSinceOldest;

                return new RateLimitResult(Allowed: false, Remaining: 0, RetryAfter: timeUntilExpiry);
            }                
        }
    }

    public class LeakyBucketRateLimiter : IRateLimiter
    {
        private readonly int _maxBucketCapacity;                    // Size of the bucket (Burst limit)
        private readonly int _tokensToLeakPerRefillCycle;           // e.g., 10 requests...
        private readonly int _leakIntervalInSeconds;                // ...per 1000 ms
        private readonly object _lock = new();

        private double _currentWaterLevel;           // Current requests in queue
        private DateTime _lastLeakTime;              // Last time we calculated a leak

        public LeakyBucketRateLimiter(int tokensToLeakPerRefillCycle, int leakIntervalInSeconds, int capacity)
        {
            _tokensToLeakPerRefillCycle = tokensToLeakPerRefillCycle;
            _leakIntervalInSeconds = leakIntervalInSeconds;
            _maxBucketCapacity = capacity;

            _currentWaterLevel = _maxBucketCapacity;
            _lastLeakTime = DateTime.UtcNow;
        }

        public RateLimitResult TryAcquire()
        {
            lock (_lock)
            {
                Leak();

                if((_currentWaterLevel+1) <= _maxBucketCapacity)
                {
                    _currentWaterLevel++;
                    return new RateLimitResult(Allowed: true, Remaining: (int)(_maxBucketCapacity - _currentWaterLevel), RetryAfter: TimeSpan.Zero);
                }
                else
                {
                    // MATH: How long until there is room for exactly 1 more request?
                    // We need the level to drop to (_maxBucketCapacity - 1)
                    double leakRatePerSecond = (double)_tokensToLeakPerRefillCycle / _leakIntervalInSeconds;

                    // Amount of water that MUST leak before we have 1 unit of space
                    double waterToWaitUnits = (_currentWaterLevel + 1) - _maxBucketCapacity;

                    double secondsToWait = waterToWaitUnits / leakRatePerSecond;

                    return new RateLimitResult(
                        Allowed: false,
                        Remaining: 0,
                        RetryAfter: TimeSpan.FromSeconds(Math.Max(secondsToWait, 0.1))
                    );
                }
            }
        }

        private void Leak() 
        {
            // Step 1: Calculate how much time has passed
            var now = DateTime.UtcNow;
            var elapsedTime = (now - _lastLeakTime).TotalSeconds;

            if (elapsedTime <= 0)
                return;

            // Step 2: Calculate the "Leak Rate" per second
            // We cast to double to ensure floating-point precision
            double leakRatePerSecond = (double)_tokensToLeakPerRefillCycle / _leakIntervalInSeconds;

            // Step 3: Calculate total water to drain based on elapsed time
            double waterToDrain = elapsedTime * leakRatePerSecond;

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

    public class RateLimiterFactory
    {
        public IRateLimiter Create(EndpointConfig config)
        {
            return config.AlgoConfig switch
            {
                TokenBucketConfig tb => new TokenBucketRateLimiter(
                    tb.TokensToRefillPerRefillCycle,
                    tb.RefillIntervalInSec,
                    tb.Capacity),

                FixedWindowConfig fw => new FixedWindowRateLimiter(
                    fw.RequestsAllowedPerWindow,
                    fw.WindowSizeInSeconds),
                _ => throw new NotImplementedException()
            };
        }
    }

    public class RateLimiterService
    {
        private readonly Dictionary<string, EndpointConfig> _endpointConfigs;
        private readonly RateLimiterFactory _rateLimiterFactory;
        private readonly ConcurrentDictionary<string, IRateLimiter> userEndpointRateLimiterMap = new();
        private readonly EndpointConfig _defaultConfig;

        public RateLimiterService(List<EndpointConfig> endpointConfigs
            , EndpointConfig defaultConfig
            , RateLimiterFactory rateLimiterFactory)
        {
            _endpointConfigs = endpointConfigs.ToDictionary(e=>e.Endpoint);
            _rateLimiterFactory = rateLimiterFactory;
            _defaultConfig = defaultConfig;
        }

        public RateLimitResult CheckRateLimit(string clientId, string endpoint)
        {
            var key = $"{clientId}:{endpoint}";

            var rateLimiter = userEndpointRateLimiterMap.GetOrAdd(key, _ =>
            {
                if (!_endpointConfigs.TryGetValue(endpoint, out var config))
                {
                    config = _defaultConfig;
                }
                return _rateLimiterFactory.Create(config);
            });

            return rateLimiter.TryAcquire();
        }
    }

    public class RateLimiterLLD
    {
        public static void Main(string[] args)
        {
            // Simple in-code configuration - easy to remember!
            var endpoints = new List<EndpointConfig>
            {
                new() {
                    Endpoint = "/search",
                    Algorithm = RateLimitAlgorithm.TokenBucket,
                    AlgoConfig = new TokenBucketConfig() { Capacity = 10, TokensToRefillPerRefillCycle = 2, RefillIntervalInSec = 1 }
                },
                new() {
                    Endpoint = "/api/users",
                    Algorithm = RateLimitAlgorithm.TokenBucket,
                    AlgoConfig = new TokenBucketConfig() { Capacity = 100, TokensToRefillPerRefillCycle = 20, RefillIntervalInSec = 1 }
                }
            };

            var defaultConfig = new EndpointConfig
            {
                Endpoint = "*",
                Algorithm = RateLimitAlgorithm.TokenBucket,
                AlgoConfig = new TokenBucketConfig() { Capacity = 50, TokensToRefillPerRefillCycle = 5, RefillIntervalInSec = 1 }
            };

            var service = new RateLimiterService(endpoints, defaultConfig, new RateLimiterFactory());
        }
    }
}