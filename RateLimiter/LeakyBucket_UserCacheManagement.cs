using System.Collections.Concurrent;

namespace LeakyBucketUserCacheManagement
{
    public abstract class RateLimiter : IDisposable
    {
        public abstract bool TryAddRequest(int id);
        public abstract void Dispose();
    }

    // Represents an individual Leaky Bucket for a single user.
    public class LeakyBucketRateLimiter_QueueBased : RateLimiter
    {
        private const int BUCKET_CAPACITY = 5; // Max requests the bucket can hold
        private const int LEAK_RATE_MS = 1000; // Milliseconds between requests processing (1 request per second)

        // A thread-safe queue to hold the requests for this specific user
        private readonly ConcurrentQueue<int> _requests = new ConcurrentQueue<int>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly int _userId;

        public LeakyBucketRateLimiter_QueueBased(int userId)
        {
            _userId = userId;
            // Start the leak process immediately when a new bucket is created
            StartLeak();
        }

        // Attempts to add a new request to the leaky bucket. This is the rate-limiting decision point.
        // Returns True if the request was allowed (queued), False if it was dropped (rate-limited).
        public override bool TryAddRequest(int requestId)
        {
            lock (_requests)
            {
                if (_requests.Count < BUCKET_CAPACITY)
                {
                    _requests.Enqueue(requestId);
                    Console.WriteLine($"[U {_userId}] Req {requestId} ADDED. Size: {_requests.Count}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[U {_userId}] Req {requestId} DROPPED (Rate-Limited). Bucket full.");
                    return false;
                }
            }
        }

        // Processes one request from the bucket (the "leak").
        private int? ProcessRequest()
        {
            if (_requests.TryDequeue(out int requestId))
            {
                Console.WriteLine($"[U {_userId}] Processing Req {requestId}");
                return requestId;
            }
            // else: Bucket is empty, nothing to process
            return null;
        }

        /// <summary>
        /// Starts the background task that simulates the periodic processing (the "leak").
        /// </summary>
        private void StartLeak()
        {
            // Start a long-running task for this specific user's bucket
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Explicitly call the method that dequeues and processes a request
                    int? processedId = ProcessRequest();

                    if (processedId == null) 
                    {
                        // Optionally log when the bucket is empty
                        Console.WriteLine("Bucket empty. Waiting for requests...");
                    }

                    try
                    {
                        // Wait for the leak rate time
                        await Task.Delay(LEAK_RATE_MS, _cancellationTokenSource.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        // Stops the background processing task (useful for cleanup).
        private void StopLeak()
        {
            _cancellationTokenSource.Cancel();
            Console.WriteLine("--- Leaky Bucket Rate Limiter Stopped ---");
        }

        public override void Dispose()
        {
            StopLeak();
        }
    }

    public class LeakyBucketRateLimiter_CounterBased : RateLimiter
    {
        private const int BUCKET_CAPACITY = 5; // Max requests the bucket can hold
        private const int LEAK_RATE_MS = 1000; // Milliseconds between requests processing (1 request per second)
        private readonly int _userId;
        private readonly object key = new object();


        //state variables
        private double _currentLevel = 0.0;
        private DateTime _lastUpdatedTimeStamp = DateTime.Now;

        public LeakyBucketRateLimiter_CounterBased(int userId)
        {
            _userId = userId;
        }

        //public override bool TryAddRequest(int requestId)
        //{
        //    bool allowed = false;
        //    lock (key)
        //    {
        //        double currentLevelPrevious = _currentLevel;
        //        TimeSpan timeElapsed = DateTime.Now - _lastUpdatedTimeStamp;
        //        double unitsLeaked = LEAK_RATE_MS * timeElapsed.TotalSeconds;
        //        _currentLevel = Math.Max(0, currentLevelPrevious - unitsLeaked);
        //        _lastUpdatedTimeStamp = DateTime.Now;

        //        if ((_currentLevel) < BUCKET_CAPACITY)
        //        {
        //            _currentLevel = _currentLevel + 1.0;
        //            //_lastUpdatedTimeStamp = DateTime.Now;
        //            allowed = true;
        //        }
        //    }

        //    if (allowed)
        //    {
        //        Console.WriteLine($"[U {_userId}] Req {requestId} ALLOWED. Level: {_currentLevel}");
        //    }
        //    else
        //    {
        //        Console.WriteLine($"[U {_userId}] Req {requestId} DROPPED (Rate-Limited). Bucket full.");
        //    }

        //    return allowed;
        //}

        public override bool TryAddRequest(int requestId)
        {
            bool allowed = false;
            lock (key)
            {
                // 1. Calculate time elapsed since the last update
                TimeSpan timeElapsed = DateTime.Now - _lastUpdatedTimeStamp;

                // 2. CORRECT CALCULATION: How many units have leaked based on elapsed time
                //    Leak = (Time Elapsed) / (Time Per Leak Unit)
                //    (e.g., 100ms elapsed / 1000ms per unit = 0.1 units leaked)
                double unitsLeaked = timeElapsed.TotalMilliseconds / LEAK_RATE_MS;

                // 3. Apply the leak to the current level
                _currentLevel = Math.Max(0, _currentLevel - unitsLeaked);

                // 4. Update the timestamp only after calculating the leak
                _lastUpdatedTimeStamp = DateTime.Now;

                if ((int)(_currentLevel + 1.0) <= BUCKET_CAPACITY) // Check if the next request fits
                {
                    _currentLevel = _currentLevel + 1.0;
                    allowed = true;
                }
            }

            // ... rest of the method (logging) ...
            if (allowed)
            {
                Console.WriteLine($"[U {_userId}] Req {requestId} ALLOWED. Level: {_currentLevel}");
            }
            else
            {
                Console.WriteLine($"[U {_userId}] Req {requestId} DROPPED (Rate-Limited). Bucket full. Level: {_currentLevel}");
            }

            return allowed;
        }

        public override void Dispose()
        {

        }
    }

    // Manages the mapping of User IDs to their specific Leaky Bucket rate limiters.
    public class RateLimiterService
    {
        // The core data structure: ConcurrentDictionary for thread-safe access
        private readonly ConcurrentDictionary<int, RateLimiter> _userBuckets = new();

        // Handles an incoming request, applying the rate limit for the specified user.
        // Returns True if the request was allowed, False if it was rate-limited.
        public bool HandleIncomingRequest(int userId, int requestId)
        {
            // 1. Get or Create the LeakyBucket for the user
            LeakyBucketRateLimiter_CounterBased bucket = (LeakyBucketRateLimiter_CounterBased)_userBuckets.GetOrAdd(
                userId,
                (id) =>
                {
                    Console.WriteLine($"\n[Manager] Creating new bucket for User {id}.");
                    return new LeakyBucketRateLimiter_CounterBased(id);
                }
            );

            // 2. Add the request to the user's bucket
            bool allowed = bucket.TryAddRequest(requestId);

            if (!allowed)
            {
                Console.WriteLine($"[U {userId}] -> Handler Action: Respond with a 429 Too Many Requests.");
            }
            else
            {
                Console.WriteLine($"[U {userId}] -> Handler Action: Request accepted (queued). Respond with 202.");
            }

            return allowed;
        }

        // Cleans up all running background leak tasks.
        public void Shutdown()
        {
            Console.WriteLine("\n[Manager] Shutting down all user buckets...");
            foreach (var bucket in _userBuckets.Values)
            {
                bucket.Dispose();
            }
        }
    }

    /*
    public class LeakyBucketUserCacheManagementDemo
    {
        
        public static void Main(string[] args)
        {
            var manager = new RateLimiterService();

            Console.WriteLine("--- Simultaneous Request Bursts for Two Users ---\n");

            // Simulate a rapid burst of 10 requests for User 1 and User 2
            for (int i = 1; i <= 10; i++)
            {
                // Request 1: User 1 attempts a request
                manager.HandleIncomingRequest(userId: 101, requestId: i);

                // Request 2: User 2 attempts a request (using higher request IDs)
                manager.HandleIncomingRequest(userId: 202, requestId: i + 10);

                Thread.Sleep(100); // Small delay between rapid requests
            }

            Console.WriteLine("\n--- Monitoring Processing for 5 seconds... ---\n");
            Thread.Sleep(5000); // Wait for processing to happen

            Console.WriteLine("\n--- New Request After Processing ---\n");
            // User 1 makes a request after their bucket has leaked some items
            manager.HandleIncomingRequest(userId: 101, requestId: 21);

            // User 3 makes their first request
            manager.HandleIncomingRequest(userId: 303, requestId: 30);

            Console.WriteLine("\n--- Monitoring Processing for 2 seconds... ---\n");
            Thread.Sleep(2000);

            manager.Shutdown();
            Console.WriteLine("\nProgram finished.");
        }        
    }*/
}


/*
    Further refinement in design:
    1.  Pass the configurations as config option.
        public class LeakyBucketOptions
        {
            public int BucketCapacity { get; init; } = 5;
            public TimeSpan LeakRate { get; init; } = TimeSpan.FromMilliseconds(1000);
        }
        
        public class LeakyBucketRateLimiter : RateLimiter 
        {
             private readonly int _bucketCapacity;
             private readonly TimeSpan _leakRate;
        
             public LeakyBucketRateLimiter(int userId, LeakyBucketOptions options)
             {
                 if (options == null) throw new ArgumentNullException(nameof(options));
                 _bucketCapacity = options.BucketCapacity;
                 _leakRate = options.LeakRate;
                 ....
             }           
        }
        
        public class RateLimiterService
        {
             private readonly LeakyBucketOptions _defaultOptions
             public RateLimiterService(LeakyBucketOptions defaultOptions)
             {
                 _defaultOptions = defaultOptions  ?? throw new ArgumentNullException(nameof(defaultOptions));
             }
             ...             
        }

        public class Main()
        {
            var config = new LeakyBucketOptions
            {
                BucketCapacity = 3, 
                LeakRate = TimeSpan.FromMilliseconds(500) //2 reqs per second
            };
            var manager = new RateLimiterService(config);
        }

    Followups:
    1. In TryAddRequest() instead of dropping the request in case of failure, maintain a request queue.
       This queue (size, type of processing order) can be passed on as config.
    2. Apply Strategy Pattern for multiple rate limiting strategies.
 */

/*
 using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;

namespace LeakyBucketUserCacheManagement
{
    // --- Helper & Config Classes (No change needed) ---

    public class LeakyBucketOptions
    {
        public int BucketCapacity { get; init; } = 5;
        public TimeSpan LeakRate { get; init; } = TimeSpan.FromMilliseconds(1000);
    }
    
    // Simplified RateLimitLease and Statistics for this example
    public class RateLimiterStatistics { public int CurrentQueuedCount { get; init; } }
    public class RateLimitLease : IDisposable { public bool IsAcquired { get; init; } public void Dispose() { } }
    
    // --- 1. STRATEGY INTERFACE (IRateLimiter) ---
    
    public interface IRateLimiter : IDisposable
    {
        bool TryAcquire(int permitCount = 1);
        RateLimiterStatistics? GetStatistics();
    }

    // --- 2. CONCRETE STRATEGY (LeakyBucketRateLimiter) ---
    
    public class LeakyBucketRateLimiter : IRateLimiter
    {
        private readonly int _bucketCapacity;
        private readonly TimeSpan _leakRate;
        private readonly ConcurrentQueue<int> _requests = new ConcurrentQueue<int>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly int _userId;

        public LeakyBucketRateLimiter(int userId, LeakyBucketOptions options)
        {
            _userId = userId;
            _bucketCapacity = options.BucketCapacity;
            _leakRate = options.LeakRate;
            StartLeak();
        }

        // Implementation of IRateLimiter contract
        public bool TryAcquire(int permitCount = 1)
        {
            if (permitCount != 1) return false; // Leaky Bucket typically handles 1 unit
            
            lock (_requests)
            {
                if (_requests.Count < _bucketCapacity)
                {
                    _requests.Enqueue(1);
                    Console.WriteLine($"[U {_userId}] 📥 Req ADDED. Size: {_requests.Count}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[U {_userId}] ❌ Req DROPPED (Rate-Limited). Bucket full.");
                    return false;
                }
            }
        }
        
        public RateLimiterStatistics? GetStatistics() => new RateLimiterStatistics
        {
            CurrentQueuedCount = _requests.Count
        };

        // Internal leak logic (similar to previous version)
        private void StartLeak()
        {
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (_requests.TryDequeue(out _))
                    {
                        // Simulate request processing/leak
                        // Console.WriteLine($"[U {_userId}] ✅ Processing request.");
                    }
                    try { await Task.Delay(_leakRate, _cts.Token); }
                    catch (TaskCanceledException) { break; }
                }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            Console.WriteLine($"[U {_userId}] --- Leaky Bucket Stopped ---");
        }
    }

    // --- 3. FACTORY INTERFACE ---

    public interface IRateLimiterFactory
    {
        IRateLimiter Create(int userId);
    }

    // --- 4. CONCRETE FACTORY (LeakyBucketFactory) ---

    public class LeakyBucketFactory : IRateLimiterFactory
    {
        private readonly LeakyBucketOptions _options;

        public LeakyBucketFactory(LeakyBucketOptions options)
        {
            _options = options;
        }

        public IRateLimiter Create(int userId)
        {
            // Factory encapsulates the creation and configuration logic
            return new LeakyBucketRateLimiter(userId, _options);
        }
    }

    // --- 5. MANAGER (CONTEXT) CLASS ---

    /// <summary>
    /// Manages user-specific rate limiters using the Factory pattern.
    /// It now stores and retrieves the IRateLimiter strategy.
    /// </summary>
    public class RateLimiterService
    {
        // Manager uses the generic interface
        private readonly ConcurrentDictionary<int, IRateLimiter> _userBuckets = new();
        
        // Manager depends on the Factory abstraction, not the concrete LeakyBucketFactory
        private readonly IRateLimiterFactory _factory; 

        public RateLimiterService(IRateLimiterFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Console.WriteLine($"[Manager] Initialized with factory: {factory.GetType().Name}.");
        }

        public bool HandleIncomingRequest(int userId, int requestId)
        {
            // 1. Get or Create the IRateLimiter using the Factory
            IRateLimiter limiter = _userBuckets.GetOrAdd(
                userId,
                (id) =>
                {
                    Console.WriteLine($"\n[Manager] 🆕 Creating new limiter for User {id} via factory.");
                    return _factory.Create(id);
                }
            );

            // 2. Use the standard Strategy method
            bool allowed = limiter.TryAcquire(1);

            if (!allowed)
            {
                Console.WriteLine($"[U {userId}] -> Handler Action: Respond with 429.");
            }
            else
            {
                Console.WriteLine($"[U {userId}] -> Handler Action: Request accepted. Stats: {limiter.GetStatistics()?.CurrentQueuedCount} queued.");
            }

            return allowed;
        }

        public void Shutdown()
        {
            Console.WriteLine("\n[Manager] Shutting down all user limiters...");
            foreach (var limiter in _userBuckets.Values)
            {
                limiter.Dispose(); // Calls the Dispose method via IRateLimiter
            }
        }
    }

    // --- 6. PROGRAM (Client) ---

    public class Program
    {
        public static void Main(string[] args)
        {
            // 1. Define configuration
            var config = new LeakyBucketOptions
            {
                BucketCapacity = 3, 
                LeakRate = TimeSpan.FromMilliseconds(500)
            };

            // 2. Create the Factory (can be swapped for a TokenBucketFactory later)
            IRateLimiterFactory factory = new LeakyBucketFactory(config);

            // 3. Create the Manager using the Factory
            var manager = new RateLimiterService(factory);

            Console.WriteLine("--- Simulation Start ---\n");

            // ... (Simulation code remains the same) ...

            for (int i = 1; i <= 8; i++)
            {
                manager.HandleIncomingRequest(userId: 101, requestId: i);
                manager.HandleIncomingRequest(userId: 202, requestId: i + 10);
                Thread.Sleep(100); 
            }

            Console.WriteLine("\n--- Monitoring Processing for 5 seconds... ---\n");
            Thread.Sleep(5000); 

            manager.Shutdown();
            Console.WriteLine("\nProgram finished.");
        }
    }
}
 
 */