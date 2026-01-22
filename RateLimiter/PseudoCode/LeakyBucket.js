/*
    Two different methods of applying this leaky bucket analogy are described in the literature.
    
    Leaky Bucket as Meter:
    ---------------------
    In this version the bucket is a counter separate from the flow of traffic.
    This counter is used only to check that the traffic or events conform to the limits: 
        The counter is incremented as each packet arrives at the point where the check is being made or an event occurs, 
        which is equivalent to the way water is added intermittently to the bucket. 
        
        The counter is also decremented at a fixed rate, equivalent to the way the water leaks out of the bucket. 
        
        As a result, the value in the counter represents the level of the water in the bucket. 
        
        If the counter remains below a specified limit value when a packet arrives or an event occurs, 
        i.e. the bucket does not overflow, that indicates its conformance to the bandwidth and burstiness 
        limits or the average and peak rate event limits. This version is referred to here as the leaky 
        bucket as a meter.


    Leaky Bucket as a Queue:
    ------------------------
    In this version, the bucket is a queue in the flow of traffic.
    This queue is used to directly control that flow: 
        Packets are entered into the queue as they arrive, equivalent to the water being added to the bucket. 
        These packets are then removed from the queue (first come, first served), usually at a fixed rate, 
        e.g. for onward transmission, equivalent to water leaking from the bucket. 
        
        This configuration imposes conformance rather than checking it, and where the output is serviced at a fixed rate 
        (and where the packets are all the same length), the resulting traffic stream is necessarily devoid of 
        burstiness or jitter. So in this version, the traffic itself is the analogue of the water passing through 
        the bucket.


    More on Leaky Bucket as a Meter/Counter:
    ----------------------------------------
    That formula is the core of the counter-based Leaky Bucket algorithm and comes directly from applying the definition 
    of the Leaky Bucket to continuous time. It represents the smoothing and decay that occurs between request arrivals.
    Here is the derivation and explanation:
    
        1.  Defining the Leaky Bucket Model 💧
            The Leaky Bucket algorithm is an analogy for a physical bucket with a hole in the bottom:
            Incoming Request (The Rain): When a request arrives, it adds water (a unit) to the bucket.
            Bucket Capacity (The Size): The bucket has a maximum capacity (capacity). 
                                        If the water exceeds this, the request is dropped.
            Leak Rate (The Hole): Water leaks out of the bucket at a constant, fixed rate (rate), 
                                  regardless of the water level (as long as it's above zero).
                                  When implementing this with counters, we need a mathematical way to simulate 
                                  the leak whenever we interact with the bucket (i.e., when a request arrives).
                                  
        2.  Derivation of the Formula 📐
            The derivation starts with calculating the two key components: the time elapsed and the resulting leakage.
            Step 1: Calculate Elapsed Time - Delta t
            The first step is to determine how long the "leak" has been running since the bucket was last checked or updated.
                Time Elapsed = time_now} - last_updated_timestamp
            
            Step 2: Calculate Units Leaked (Decay)
            Since the leak rate is constant, the total number of units (requests) that have left the bucket during 
            the elapsed time is a simple multiplication:
                Units Leaked = Time Elapsed X rate
            
            Step 3: Calculate the New LevelTo find the bucket's current true level, you subtract the leaked units 
            from the level recorded during the previous update:
                New Level = current_level_previous - Units Leaked}
                
            Step 4: Apply the Non-Negative Constraint (The max(0, ...) function)
            In a physical leaky bucket, the water level can never go below zero. The bucket stops leaking when 
            it is empty. Therefore, we must clamp the new level at a minimum of zero. This is where the max(0, ...) function comes in:
            current_level_new = max(0, New Level)
            
            3. Final Formula Assembly
            Substituting the intermediate calculations back into the final formula gives you the exact expression:
            current_level_new = max(0, current_level_previous - (time_now - last_updated_timestamp) x rate)
            
            This formula effectively smooths the usage count by instantaneously calculating the decay up to the 
            precise moment the current request arrives, making it an efficient and mathematically sound implementation 
            of the Leaky Bucket principle without relying on a separate background thread or queue.

            We apply the non-negative constraint using the max(0, ...) function because it mathematically models 
            the physical reality of a Leaky Bucket. The bucket represents a limited resource or capacity, and 
            its "level" (the number of current requests/units) can never be less than zero.
            Here's why this constraint is crucial:
                1.  Physical Constraint Simulation 
                    The Leaky Bucket algorithm's core principle is that the "leak" stops once the bucket is empty.
                    If the previous level was 5 units, and the time elapsed results in 10 units of leakage, the pure 
                    subtraction would give a result of 5 - 10 = -5.A negative level of -5 is meaningless in this context. 
                    It suggests the bucket is somehow drawing requests in or has "negative capacity," which isn't 
                    part of the rate limiting model.By using max(0, ...), we ensure that if the decay calculation 
                    results in any negative number, the current_level is clamped back to 0, correctly simulating the bucket 
                    stopping its leak when empty.
                    
                2.  Preventing "Credit" Accumulation 🚫
                    The non-negative constraint prevents the rate limiter from accumulating "credit" that can be used later.
                    If the level were allowed to go negative (e.g., to -5), the next 5 incoming requests would be 
                    immediately granted because they would only bring the level from -5 up to 0. 
                    This effectively means the user can utilize the capacity for requests that should have been processed 
                    during the idle time plus 5 more, potentially leading to a burst larger than the defined capacity.
                    
                    The Leaky Bucket is designed for rate smoothing, not for pre-paid bursts (which is the domain of 
                    the Token Bucket algorithm). By setting the floor at 0, the next request always starts filling the 
                    bucket from empty, respecting the constant leak rate.The resulting formula, 
                    max(0, current_level - leakage), guarantees that the bucket level is always a valid 
                    physical quantity, preventing unexpected rate limit bypasses.


        The counter-based approach simplifies the code and avoids the complexities of dedicated per-user background threads, 
        significantly improving scalability.
*/


const BUCKET_CAPACITY = 10; // Max requests the bucket can hold
const LEAK_RATE = 1000;     // Milliseconds between requests processing

let leakyBucket = {
    requests: [],
    addRequest: function(requestId) {
        if (this.requests.length < BUCKET_CAPACITY) {
            this.requests.push(requestId);
            console.log(`📥 Request ${requestId} added.`);
            return true;
        } else {
            console.log(`❌ Bucket full. Request ${requestId} dropped.`);
            return false;
        }
    },
    processRequest: function() {
        if (this.requests.length > 0) {
            const requestId = this.requests.shift();
            console.log(`✅ Processing Request ${requestId}`);
            return true;
        }
        return false;
    }
};

function handleIncomingRequest(requestId) {
    if (!leakyBucket.addRequest(requestId)) {
        console.log('Request could not be processed. Try again later.');
    }
}

setInterval(() => {
    leakyBucket.processRequest();
}, LEAK_RATE); // Processes one request per LEAK_RATE
