using System.Collections.Concurrent;

/*
    roblem Statement:
    "You're building an in-memory rate limiter for an API gateway. 
    The system receives configuration from an external service that provides rate limiting rules per endpoint.

    Each endpoint can have its own limit with a specific algorithm. Here's an example configuration for one endpoint:
    {
    "endpoint": "/search",
    "algorithm": "TokenBucket",
    "algoConfig": {
        "capacity": 1000,
        "refillRatePerSecond": 10
    }
    }

    This config allows bursts up to 1000 requests, refilling at 10 requests per second.
    Your job is to build the in-memory rate limiter that enforces these rules."



    Functional Requirements:
    1. Configuration is provided at startup (loaded once)
    2. System receives requests with (clientId: string, endpoint: string)
    3. Each endpoint has a configuration specifying:
    - Algorithm to use (e.g., "TokenBucket", "SlidingWindowLog", etc.)
    - Algorithm-specific parameters (e.g., capacity, refillRatePerSecond for Token Bucket)
    4. System enforces rate limits by checking clientId against the endpoint's configuration
    5. Return structured result: (allowed: boolean, remaining: int, retryAfterMs: long | null)
    6. If endpoint has no configuration, use a default limit
    7. System should be Thread safe and efficient.
    8. Extend the system to not only limit users, but also services, IPs
    9. The system should Rate Limit the users based on UserId and their tier(free or premium or more)
*/

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
                if(timeToNextRefill > TimeSpan.Zero)
                {
                    return new RateLimitResult(Allowed: false, 0, RetryAfter: timeToNextRefill);
                }
                else
                {
                    return new RateLimitResult(Allowed: false, 0, RetryAfter: TimeSpan.FromSeconds(_refillIntervalInSeconds));
                }
            }
        }

        /*
            - every 2 seconds 10 tokens have to be refilled
            - let's say 6 seconds have elapsed.
            - so basically 6/2 = 3 refill cycles have passed.
            - that means 3 * 10 = 30 tokens should have been refilled.
         */
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

    /*
     The Scenario
        Window Size: 60 seconds.
        Allowed Requests: 2.

        The Timeline:
        Request A arrives at 12:00:00. (Allowed, Queue =)
        Request B arrives at 12:00:10. (Allowed, Queue =)
        Request C arrives at 12:00:15. (Queue is full! Entering Rejection Logic...)

        Step-by-Step Logic at 12:00:15
        1. var oldestRequestTime = _requestTimeStampsQ.Peek();
        We look at the front of the queue to find the first request that is "blocking" us.
        Result: oldestRequestTime is 12:00:00 (Request A).

        2. var timeSinceOldest = now - oldestRequestTime;
        We calculate how much time has passed since that oldest request happened.
        Math: 12:00:15 - 12:00:00 = 15 seconds.
        Meaning: Request A has been in our window for 15 seconds already.

        3. var timeUntilExpiry = TimeSpan.FromSeconds(_windowSizeInSeconds) - timeSinceOldest;
        Since the window is 60 seconds long, Request A will "expire" (exit the window) exactly 60 seconds after it started.
        Math: 60s (Window) - 15s (Time Passed) = 45 seconds.
        Meaning: In exactly 45 seconds, Request A will be older than 60 seconds, it will be dequeued, and a new slot will open.
     
     */
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

    public class Program
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