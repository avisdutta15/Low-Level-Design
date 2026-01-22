using System.Collections.Concurrent;

namespace LeakyBucketSpillOverQueue
{
    // --- Configuration ---

    public enum SpilloverQueueType { FIFO, LIFO }

    public class RateLimiterOptions
    {
        // Core Leaky Bucket settings
        public int BucketCapacity { get; init; } = 5;
        public TimeSpan LeakRate { get; init; } = TimeSpan.FromMilliseconds(1000);

        // Spillover Queue settings
        public int SpilloverQueueCapacity { get; init; } = 10;
        public SpilloverQueueType SpilloverQueueOrder { get; init; } = SpilloverQueueType.FIFO;
    }

    // --- Queue Abstraction (Strategy Pattern for Queue Order) ---

    public interface IRequestQueue
    {
        bool TryEnqueue(int requestId);
        bool TryDequeue(out int requestId);
        int Count { get; }
        int Capacity { get; }
        bool IsFull { get; }
    }

    // FIFO Implementation (uses Queue<T>)
    public class FifoRequestQueue : IRequestQueue
    {
        private readonly Queue<int> _queue = new Queue<int>();
        public int Capacity { get; }
        public int Count => _queue.Count;
        public bool IsFull => _queue.Count >= Capacity;

        public FifoRequestQueue(int capacity)
        {
            Capacity = capacity;
        }
        public bool TryEnqueue(int requestId)
        {
            if (IsFull) return false;
            _queue.Enqueue(requestId);
            return true;
        }

        public bool TryDequeue(out int requestId) => _queue.TryDequeue(out requestId);
    }

    // LIFO Implementation (uses Stack<T>)
    public class LifoRequestQueue : IRequestQueue
    {
        private readonly Stack<int> _stack = new Stack<int>();
        public int Capacity { get; }
        public int Count => _stack.Count;
        public bool IsFull => _stack.Count >= Capacity;

        public LifoRequestQueue(int capacity) => Capacity = capacity;

        public bool TryEnqueue(int requestId)
        {
            if (IsFull) return false;
            _stack.Push(requestId); // Stack uses Push
            return true;
        }

        public bool TryDequeue(out int requestId)
        {
            if (_stack.TryPop(out requestId)) // Stack uses TryPop
            {
                return true;
            }
            requestId = default;
            return false;
        }
    }

    // --- Queue Factory ---

    public static class RequestQueueFactory
    {
        public static IRequestQueue CreateQueue(RateLimiterOptions options)
        {
            return options.SpilloverQueueOrder switch
            {
                SpilloverQueueType.FIFO => new FifoRequestQueue(options.SpilloverQueueCapacity),
                SpilloverQueueType.LIFO => new LifoRequestQueue(options.SpilloverQueueCapacity),
                _ => throw new ArgumentException("Invalid queue type.")
            };
        }
    }

    // --- Base Rate Limiter ---

    public abstract class RateLimiter : IDisposable
    {
        public abstract bool TryAddRequest(int id);
        public abstract void Dispose();
    }

    // --- Leaky Bucket Implementation ---

    /// <summary>
    /// Represents an individual Leaky Bucket for a single user, with a spillover queue.
    /// </summary>
    public class LeakyBucketRateLimiter : RateLimiter
    {
        // Core bucket state (for currently processing requests - MUST be FIFO)
        private readonly ConcurrentQueue<int> _processingQueue = new ConcurrentQueue<int>();
        private readonly IRequestQueue _spilloverQueue; // The new configurable queue

        private readonly int _bucketCapacity;
        private readonly TimeSpan _leakRate;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly int _userId;
        private readonly string _queueType;

        public LeakyBucketRateLimiter(int userId, RateLimiterOptions options)
        {
            _userId = userId;
            _bucketCapacity = options.BucketCapacity;
            _leakRate = options.LeakRate;

            // Create the configurable spillover queue
            _spilloverQueue = RequestQueueFactory.CreateQueue(options);
            _queueType = options.SpilloverQueueOrder.ToString();

            StartLeak();
        }

        /// <summary>
        /// Attempts to add a new request. If the main bucket is full, it queues to spillover.
        /// </summary>
        public override bool TryAddRequest(int requestId)
        {
            // 1. Try to add to the main processing queue (the actual Leaky Bucket)
            if (_processingQueue.Count < _bucketCapacity)
            {
                _processingQueue.Enqueue(requestId);
                Console.WriteLine($"[U {_userId}] 📥 Req {requestId} ADDED to Bucket. Size: {_processingQueue.Count}");
                return true;
            }

            // 2. If bucket is full, try to add to the configurable spillover queue
            lock (_spilloverQueue) // Lock access to the non-ConcurrentQueue implementation
            {
                if (_spilloverQueue.TryEnqueue(requestId))
                {
                    Console.WriteLine($"[U {_userId}] ⚠️ Req {requestId} ADDED to Spillover Queue ({_queueType}). Size: {_spilloverQueue.Count}/{_spilloverQueue.Capacity}");
                    return true;
                }
            }

            // 3. If both bucket and spillover queue are full, drop the request
            Console.WriteLine($"[U {_userId}] ❌ Req {requestId} DROPPED. Both Bucket and Spillover Queue are full.");
            return false;
        }

        // Processes one request from the system (leak)
        private int? ProcessRequest()
        {
            // 1. Always check the main processing queue first (FIFO)
            if (_processingQueue.TryDequeue(out int requestId))
            {
                Console.WriteLine($"[U {_userId}] ✅ Processing Req {requestId} from Bucket.");
                return requestId;
            }

            // 2. If the bucket is empty, pull one request from the spillover queue 
            //    and place it back into the main processing queue (to be processed next leak cycle).
            //    This effectively moves requests from the spillover queue into the bucket.
            lock (_spilloverQueue)
            {
                if (_spilloverQueue.TryDequeue(out int spilloverId))
                {
                    _processingQueue.Enqueue(spilloverId);
                    Console.WriteLine($"[U {_userId}] 🔁 Spillover Req {spilloverId} moved to Bucket from {_queueType}.");
                }
            }

            // Re-attempt to process from the main queue (in case the move was successful)
            // Note: In this specific implementation, it processes the next item in the next cycle,
            // which is cleaner for demonstration than recursive check. We return null if the bucket was initially empty.
            return null;
        }

        /// <summary>
        /// Starts the background task that simulates the periodic processing (the "leak").
        /// </summary>
        private void StartLeak()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    int? processedId = ProcessRequest();

                    if (processedId == null)
                    {
                        // Check if the overall system is empty
                        if (_processingQueue.IsEmpty && _spilloverQueue.Count == 0)
                        {
                            Console.WriteLine($"[U {_userId}] ⏸️ System empty. Waiting for requests...");
                        }
                    }

                    try { await Task.Delay(_leakRate, _cancellationTokenSource.Token); }
                    catch (TaskCanceledException) { break; }
                }
            });
        }

        public override void Dispose()
        {
            _cancellationTokenSource.Cancel();
            Console.WriteLine($"[U {_userId}] --- Leaky Bucket Rate Limiter Stopped ---");
        }
    }

    // --- Manager (Modified to use config) ---

    public class UserRateLimiterManager
    {
        private readonly ConcurrentDictionary<int, LeakyBucketRateLimiter> _userBuckets = new();
        private readonly RateLimiterOptions _defaultOptions;

        public UserRateLimiterManager(RateLimiterOptions defaultOptions)
        {
            _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
        }

        public bool HandleIncomingRequest(int userId, int requestId)
        {
            LeakyBucketRateLimiter bucket = _userBuckets.GetOrAdd(
                userId,
                (id) => new LeakyBucketRateLimiter(id, _defaultOptions)
            );

            bool allowed = bucket.TryAddRequest(requestId);

            if (!allowed)
            {
                Console.WriteLine($"[U {userId}] -> Handler Action: Respond with a 429 Too Many Requests.");
            }

            return allowed;
        }

        public void Shutdown()
        {
            Console.WriteLine("\n[Manager] Shutting down all user buckets...");
            foreach (var bucket in _userBuckets.Values)
            {
                bucket.Dispose();
            }
        }
    }

    // --- Demo Program (Sets config) ---

    public class LeakyBucketSpillOverQueueDemo
    {
        /*
        public static void Main(string[] args)
        {
            // 1. Define the configuration for the system
            var config = new RateLimiterOptions
            {
                BucketCapacity = 3,                       // Max 3 requests processed immediately
                LeakRate = TimeSpan.FromMilliseconds(500),// 2 requests/sec leak rate
                SpilloverQueueCapacity = 5,               // Secondary queue size
                SpilloverQueueOrder = SpilloverQueueType.LIFO // LIFO: Prioritizes newer requests when the queue is full
            };

            var manager = new UserRateLimiterManager(config);

            Console.WriteLine($"\n--- Simulation Start (Bucket: {config.BucketCapacity}, Leak: {config.LeakRate.TotalSeconds}s, Spillover: {config.SpilloverQueueOrder} up to {config.SpilloverQueueCapacity}) ---\n");

            // Simulate a rapid burst of 10 requests (Bucket capacity 3, Spillover 5. Total capacity 8)
            for (int i = 1; i <= 10; i++)
            {
                // User 1 will fill the bucket (R1-R3), fill the spillover (R4-R8), and drop the rest (R9-R10)
                manager.HandleIncomingRequest(userId: 101, requestId: i);
                Thread.Sleep(50); // Small delay to show the burst
            }

            Console.WriteLine("\n--- Monitoring Processing for 5 seconds... ---\n");
            Thread.Sleep(5000);

            manager.Shutdown();
            Console.WriteLine("\nProgram finished.");
        }
        */
    }
}