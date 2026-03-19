# Rate Limiting Algorithms

## 1. Token Bucket

 - This is the industry standard (used by Amazon and Stripe) because it balances flexibility with control.

 - **How it works:** You have a bucket of tokens. Each request takes a token. Tokens refill over time.
 
 - **Pros:**

	- **Allows Bursts:** If a user has been inactive, their bucket fills up. They can make a rapid burst of requests (up to _bucketLimit) without being blocked. This is great for user experience (e.g., loading a dashboard).
	- **Efficient:** Very low memory and CPU usage.

 - **Cons:** Slightly more complex to implement than Fixed Window.

 - **Use Case:** Most public APIs where you want to allow legitimate users to burst occasionally but cap their long-term average.

 - **Implementation:**
 
```csharp
    public bool TryAcquire()
    {
        lock (_lock)
        {
            Refill();

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
```
 - **Key Logic Changes Explained**
    - /*
            - every 2 seconds 10 tokens have to be refilled
            - let's say 6 seconds have elapsed.
            - so basically 6/2 = 3 refill cycles have passed.
            - that means 3 * 10 = 30 tokens should have been refilled.
         */ 
    - **cyclesPassed Calculation:** Instead of multiplying time, we divide elapsedTime / _refillIntervalMs. 
                              If your interval is 1000ms and 2500ms have passed, cyclesPassed is 2.

    - **Time Drift Prevention:**
      1. If we set _lastRefillTime = DateTime.UtcNow, we lose the extra 500ms from the example above (2500ms elapsed vs 2000ms 
                                accounted for).
      2. By doing _lastRefillTime.AddMilliseconds(cyclesPassed * _refillIntervalMs), that "remainder" 500ms stays in the 
    calculation for the next call, ensuring your rate is mathematically precise over long periods.

    - **Calculate Retry After Time:**  
      1.//if you run out of tokens, it’s because you are currently inside a refill cycle and waiting for the next one to finish.
            //Example: Your interval is 10 seconds. You checked at 8 seconds.
            //Math: 10s (Interval) - 8s (Elapsed) = 2s.
            //Result: 2s is greater than zero, so the user is told to wait exactly 2 seconds.
      2. //If timeToNextRefill is zero or negative, it means:
                //A refill is due right now.
                //However, because we are in this block, we know _tokens is still 0.
                //Instead of telling the user to Retry - After: 0.00001 seconds(which would cause them to spam your CPU immediately), we provide a full cycle penalty.
            

## 2. Leaky Bucket

Think of this as a funnel. No matter how much water you pour in at the top, it comes out the bottom at a steady, constant trickle.

 - **How it works:** Requests enter a queue (bucket). They are processed (leaked) at a constant rate. If the queue is full, new requests are dropped.
 - **Pros:**
   - **Smooths Traffic:** It forces a constant outflow rate. This protects downstream services (like your database) from getting hammered by spikes.
 - **Cons:**
   - **"Lag" Perception:** If the bucket is full, valid requests might be rejected immediately even if the server is currently idle, because the leak rate is fixed.

 - **Use Case:** Background job processing, writing to databases with limited IOPS, or any system that needs a stable, predictable load.

 - **Implementation:**

```csharp
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
```


1. The Unitary Math formulaThe precise formula for RetryAfter in a Leaky Bucket is:
$$\text{Seconds to Wait} = \frac{(\text{CurrentLevel} + 1) - \text{Capacity}}{\text{LeakRate}}$$2. Why it varies (The "Micro-Overflow" Scenario)Imagine your Leak Rate is 1 unit per second (meaning 1 request is "leaked" every 1000ms).Scenario A: The Perfect Full BucketCapacity: 10.0Current Level: 10.0 (Exactly full)Math: $\frac{(10 + 1) - 10}{1} = \frac{1}{1} = \mathbf{1.0s}$Wait: You must wait for the full duration of one leak cycle.Scenario B: The "Just About to Leak" BucketImagine a request arrives at 10.0 capacity, but it's been 900ms since the last leak calculation.Leak() runs first: $10.0 - (0.9s \times 1\text{ unit/sec}) = \mathbf{9.1}$Request arrives: $9.1 + 1 = 10.1$ (Still an overflow!)Math: $\frac{10.1 - 10.0}{1} = \mathbf{0.1s}$Wait: You only wait 0.1 seconds. Why? Because the bucket was almost ready to drop a unit anyway.

https://gemini.google.com/app/383c2b58c4899192?hl=en-IN

## 3. Fixed Window Counter
The simplest approach. "You get 100 requests between 12:00 and 12:01."

 - **How it works:** A counter increments for the current time window. At the start of the next window, the counter resets to 0.

 - **Pros:** Easiest to implement and debug. Lowest memory footprint.

 - **Cons:**

   - **Boundary Issue:** If a user makes 100 requests at 12:00:59 and 100 more at 12:01:01, they have made 200 requests in 2 seconds, effectively doubling the allowed rate for that brief period.

 - **Use Case:** Basic rate limiting where strict precision isn't required, or for internal services where you trust the clients.
 - **Implementation:**

```csharp
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
```

Gemini said
You've spotted a very specific logical "trap" in how Fixed Window limiters handle the transition point.

If elapsedSeconds is exactly 0 (which happens immediately after a reset), then retryAfter will indeed be the full _windowSizeInSeconds.

While this looks wrong at first glance, it is actually mathematically consistent with how a Fixed Window works. Here is why:

1. The Scenario
Imagine a window of 60 seconds allowing 1 request.

T=0s: Request 1 arrives. elapsedSeconds = 0. _requestsMade becomes 1. Allowed.

T=1s: Request 2 arrives. elapsedSeconds = 1. _requestsMade is already 1. Blocked.

RetryAfter = 60 - 1 = 59s. (Correct)

T=60s: Request 3 arrives. elapsedSeconds = 60.

The if (elapsedSeconds >= _windowSizeInSeconds) trigger hits.

_windowStartTime is reset to "Now".

_requestsMade is reset to 0.

Crucially: elapsedSeconds is updated to 0.

Allowed.

2. When does RetryAfter actually return 60?
The only time a user sees RetryAfter = 60 is if they exhaust the entire bucket in the exact same millisecond the window starts.

Window starts at 12:00:00.

User sends 100 requests at 12:00:00.001.

The 101st request arrives at 12:00:00.002.

The math says: 60 - 0.002 = 59.998s.

Because the window just started and they already spent their "allowance," they really do have to wait almost the entire 60 seconds for the next reset.

3. The "Smallest Possible Value" Edge Case
If you are worried about the user seeing a "60 second wait" when the window is actually about to reset (due to a race condition or a very slight lag), you can use a Math.Max or a small offset.

However, in a Fixed Window, the logic is binary: You are either in the current window (and must wait for it to end) or you have moved to the new window (and the count is zero).



## 4. Sliding Window Log
The most accurate but most expensive approach.
 - **How it works:** Keeps a log (queue) of timestamps for every request. Removes timestamps older than the window.
 - **Pros:**
   - **100% Accurate:** No "boundary issues." It strictly enforces the limit over any rolling time window.

 - **Cons:**
   - **Memory Heavy:** Stores a timestamp for every request. If you allow 1 million requests/hour, you need to store 1 million timestamps per user.
   - **Slow Cleanup:** Iterating through the queue to remove old timestamps can be slow under high load.

 - **Use Case:** Security-critical limits with low volume, such as "Max 5 failed login attempts per minute" or "Max 10 OTP requests per day."

 - **Implementation:**

```csharp
    private Queue<DateTime> _requestTimeStamps = new Queue<DateTime>();

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
```

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

4. The "Safety" Check
var retryAfter = timeUntilExpiry > TimeSpan.Zero ? timeUntilExpiry : TimeSpan.FromMilliseconds(100);
If the math results in 0 or a negative number (due to the CPU processing the request exactly at the 60.000s mark), we return a tiny 100ms delay. This prevents the client from entering a "tight loop" and smashing your CPU with retries.

## Quick Comparison Table

| Algorithm | Mechanism | Burst Handling | Memory Usage | Accuracy | Best For |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Token Bucket** | Tokens refill at set rate | ✅ **Excellent** (Allows bursts up to capacity) | Low ($O(1)$) | High | **General Purpose APIs** (User-centric) |
| **Leaky Bucket** | Queue drains at constant rate | ⚠️ **Strict** (Smooths bursts into constant flow) | Low ($O(1)$) | High | **Traffic Shaping** (Protecting Databases/Background Jobs) |
| **Fixed Window** | Counter resets every window | ❌ **Poor** (Can allow 2x limit at boundaries) | Lowest ($O(1)$) | Medium | **Basic DDoS Protection** / Low priority services |
| **Sliding Log** | Tracks every request timestamp | ✅ **Good** (Accurate rolling window) | High ($O(N)$) | **Perfect** | **Strict, Low-Volume APIs** (e.g., "3 failed logins per hour") |

## Recommendation
- **Default Choice:** Start with **Token Bucket**. It handles user behavior best.

- **Strict Protection:** Use **Leaky Bucket** if you are protecting a fragile downstream resource (like a legacy database).

- **Strict Rules:** Use **Sliding Window Log** only for low-volume, high-precision rules (like login attempts).

![Rate Limiter LLD](./assets/rate-limiter-lld.png)

```csharp
    enum ClientType{
		User, Service, IP
	}

	enum TierType{
		FREE, PREMIUM, INTERNAL
	}

	class ClientIdentity{
		string Id {get; set;}
		ClientType clientType {get; set;}
		TierType tier{get; set;}

		public override string ToString() => return $"{clientType:tierType}";
	}

	class RateLimitRule
	{
		public RateLimitAlgorithm RateLimitAlgorithm { get; set; }

		/* Algorithm specific properties */
		public int TokensToRefillPerRefillCycle { get; set; } 
		public int RefillIntervalMs { get; set; }
		public int BucketLimit { get; set; }
		public int RequestsAllowedPerWindow { get; set; }
		public int WindowSizeInMs { get; set; }
		public int outflowTokens { get; set; }
		public int leakIntervalMs { get; set; }
	}

	class RuleStore {
		//We can get the rules from Db or some other data-stores
		public RateLimitRule? GetRule(ClientIdentity client)
		{
			// Example logic: Different rules for different Tiers and Types
			if (client.ClientType == ClientType.IP)
				return new RateLimitRule { RateLimitAlgorithm = RateLimitAlgorithm.FixedWindow, RequestsAllowedPerWindow = 10, WindowSizeInMs = 1000 };

			return client.Tier switch
			{
				TierType.Premium => new RateLimitRule { RateLimitAlgorithm = RateLimitAlgorithm.TokenBucket, BucketLimit = 1000, TokensToRefillPerRefillCycle = 10, RefillIntervalMs = 100 },
			};
		}
	}

	class RateLimitFactory {
	    public static IRateLimiter Create(RateLimitRule rule)
		{
			return rule.RateLimitAlgorithm switch
			{
				RateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(tokensToRefillPerRefillCycle: rule.TokensToRefillPerRefillCycle, refillIntervalMs: rule.RefillIntervalMs, bucketLimit: rule.BucketLimit),
				RateLimitAlgorithm.LeakyBucket => new LeakyBucketRateLimiter(outflowTokens: rule.outflowTokens, leakIntervalMs: rule.leakIntervalMs, bucketLimit: rule.BucketLimit),
				RateLimitAlgorithm.FixedWindow => new FixedWindowRateLimiter(requestsAllowedPerWindow: rule.RequestsAllowedPerWindow, windowSizeInMs: rule.WindowSizeInMs),
				RateLimitAlgorithm.SlidingWindow => new SlidingWindowLogRateLimiter(requestsAllowedPerWindow: rule.RequestsAllowedPerWindow, windowSizeInMs: rule.WindowSizeInMs),
				_ => throw new NotImplementedException()
			};
		}

	}

	class Service{
		ConcurrentDictionary<string, IRateLimiter> _cache;
		RuleStore _ruleStore
		Service(ruleStore) : _ruleStore;

		bool IsAllowed(ClientIdentity client)
		{
			string key = client.ToString();

			IRateLimiter rateLimiter = _cache.GetOrAdd(key, _ =>
			{
				// This lambda only runs if the key doesn't exist
				var rule = _ruleStore.GetRule(client);
				return RateLimiterFactory.Create(rule);
			});

			return rateLimiter.TryAcquire();
		}
	}

	class client{
		RuleStore store = new();
		RateLimiterService service = new(store);

		var freeUser = new ClientIdentity { Id = "user_123", ClientType = ClientType.User, Tier = TierType.Free };
        service.IsAllowed(freeUser);
	}
```
