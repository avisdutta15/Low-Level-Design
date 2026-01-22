using System.Collections.Concurrent;

namespace LeakyBucket_Simple
{
    public abstract class RateLimiter
    {
        public abstract bool TryAddRequest(int id);
    }

    public class LeakyBucketRateLimiter : RateLimiter
    {
        private const int BUCKET_CAPACITY = 10; // Max requests the bucket can hold
        private const int LEAK_RATE_MS = 1000; // Milliseconds between requests processing (1 request per second)

        // A thread-safe collection to hold the requests
        private readonly ConcurrentQueue<int> _requests = new ConcurrentQueue<int>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public LeakyBucketRateLimiter() 
        {
            // Start the leak process immediately when a new bucket is created
            StartLeak();
        }

        // Attempts to add a new request to the leaky bucket. This is the rate-limiting decision point.
        // Returns True if the request was allowed into the bucket, False if it was dropped (rate-limited).
        public override bool TryAddRequest(int requestId)
        {
            // Use a lock to ensure thread-safe check of count and enqueue operation.
            lock (_requests)
            {
                if (_requests.Count < BUCKET_CAPACITY)
                {
                    _requests.Enqueue(requestId);
                    Console.WriteLine($"Request {requestId} ADDED to bucket. Current size: {_requests.Count}");
                    // The request was ALLOWED (queued for later processing)
                    return true;
                }
                else
                {
                    Console.WriteLine($"Bucket full. Request {requestId} DROPPED (Rate-Limited). Current size: {_requests.Count}");
                    // The request was DROPPED
                    return false;
                }
            }
        }


        // Processes one request from the bucket (the "leak").
        // returns The processed request ID, or null if the bucket was empty.
        private int? ProcessRequest()
        {
            if (_requests.TryDequeue(out int requestId))
            {
                Console.WriteLine($"Processing Request {requestId}");
                // Return the ID of the request that was processed (allowed to pass)
                return requestId;
            }

            // Return null to indicate the bucket was empty (no request processed)
            return null;
        }


        // Starts the background task that simulates the periodic processing (the "leak").
        // This task **continuously dequeues** requests at the fixed rate.
        private void StartLeak()
        {
            Console.WriteLine($"--- Leaky Bucket Rate Limiter Started (Rate: 1 request per {LEAK_RATE_MS}ms) ---");

            // Start a long-running task to simulate setInterval
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
                        // Expected when StopLeak is called
                        break;
                    }
                }
            });
        }


        // Stops the background processing task.
        public void StopLeak()
        {
            _cancellationTokenSource.Cancel();
            Console.WriteLine("--- Leaky Bucket Rate Limiter Stopped ---");
        }
    }

    public class LeakyBuclet_SimpleDemo
    {
        public static void Main1(string[] args)
        {
            var limiter = new LeakyBucketRateLimiter();

            Console.WriteLine("\nSimulating Incoming Requests (Rapid Burst):");

            // Simulate a rapid burst of 15 requests
            for (int i = 1; i <= 15; i++)
            {
                bool allowed = limiter.TryAddRequest(i);

                // This is where you capture the result: was it allowed (true) or dropped (false)?
                if (!allowed)
                {
                    Console.WriteLine($"-> Handler Action: Request {i} could not be processed. Respond with a 429 error.");
                }
                else
                {
                    Console.WriteLine($"-> Handler Action: Request {i} accepted. Respond with a 202 status.");
                }
                Thread.Sleep(100); // Small delay to visualize output
            }

            Console.WriteLine("\nMonitoring request processing for 5 seconds...\n");
            Thread.Sleep(5000);

            limiter.StopLeak();
            Console.WriteLine("\nProgram finished.");
        }
    }

}