//using System.Collections.Concurrent;

//namespace LeakyBucketTestCode
//{

//    public abstract class RateLimiter : IDisposable
//    {
//        public abstract bool AllowRequest(int userId);
//        public abstract void Dispose();
//    }

//    public class LeakyBucketRateLimiter : RateLimiter
//    {
//        private readonly TimeSpan _refrestRate = TimeSpan.FromMilliseconds(1000);
//        private readonly int _tokenLimit = 10;
//        private readonly int _userId;
//        private readonly ConcurrentQueue<int> _requests = new();
//        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

//        public LeakyBucketRateLimiter(int userId)
//        {
//            _userId = userId;
//            StartLeak();
//        }
//        public override bool AllowRequest(int requestId)
//        {
//            lock (_requests)
//            {
//                if (_requests.Count < _tokenLimit)
//                {
//                    _requests.Enqueue(requestId);
//                    Console.WriteLine($"User: {_userId} RequestId: {requestId} ADDED. Size : {_requests.Count}");
//                    return true;
//                }
//                else
//                {
//                    Console.WriteLine($"User: {_userId} RequestId: {requestId} DROPPED. Size : {_requests.Count}");
//                    return false;
//                }
//            }
//        }
//        private int? ProcessRequest()
//        {
//            //if we have requests to process then process them
//            if(_requests.Count != 0)
//            {
//                _requests.TryDequeue(out int requestId);
//                Console.WriteLine($"Processing Request for userId : {_userId} and requestId : {requestId}");
//                return requestId;
//            }
//            return null;
//        }

//        private void StartLeak()
//        {
//            Task.Run(async () => {
//                while (!_cancellationTokenSource.Token.IsCancellationRequested)
//                {
//                    int? requestId = ProcessRequest();
//                    if (requestId == null)
//                    {
//                        Console.WriteLine("Bucket is empty. Waiting for requests");
//                    }

//                    try
//                    {
//                        await Task.Delay(_refrestRate, _cancellationTokenSource.Token);
//                    }
//                    catch (TaskCanceledException)
//                    {
//                        break;
//                    }                    
//                } 
//            });
//        }
//        private void StopLeak()
//        {
//            _cancellationTokenSource.Cancel();
//        }
//        public override void Dispose()
//        {
//            StopLeak();
//        }
//    }

//    public class RateLimiterService
//    {
//        private readonly ConcurrentDictionary<int, RateLimiter> _userRateLimiters = new();
//        public RateLimiterService()
//        {

//        }

//        public bool TryAllowRequest(int userId, int requestId)
//        {
//            var limiter = _userRateLimiters.GetOrAdd(userId, (_)=> {
//                Console.WriteLine($"[Manager] Creating new bucket for User {userId}");
//                return new LeakyBucketRateLimiter(userId);
//            });
//            return limiter.AllowRequest(requestId);
//        }

//        public void ShutDown()
//        {
//            foreach(var rateLimiter in _userRateLimiters.Values)
//            {
//                rateLimiter.Dispose();
//            }
//        }
//    }

//    public class LeakyBucketTestCodeDemo
//    {
//        public static void Main(string[] args) 
//        { 
//            var rateLimiterService = new RateLimiterService();

//            //Simulate a burst of requests for user 1 and user 2
//            for(int i=0; i<20; i++)
//            {
//                rateLimiterService.TryAllowRequest(userId: 101, requestId: i);
//                rateLimiterService.TryAllowRequest(userId: 201, requestId: i+10);
//                Thread.Sleep(100);
//            }

//            Thread.Sleep(5000);
//        }
//    }
//}
