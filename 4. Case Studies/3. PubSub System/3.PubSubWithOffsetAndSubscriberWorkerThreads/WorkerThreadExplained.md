# Worker Thread Run() Method - Detailed Explanation

## The Core Loop

```csharp
public void Run()
{
    lock (_topicSubscriber)  // 🔒 Lock on the subscriber's offset object
    {
        while (true)  // ♾️ Infinite loop - worker runs forever
        {
            try
            {
                int curOffset = _topicSubscriber.Offset;

                // WAIT PHASE: Sleep if no new messages
                while (curOffset >= _topic.Messages.Count)
                {
                    Monitor.Wait(_topicSubscriber);  // 😴 Sleep here
                    curOffset = _topicSubscriber.Offset;
                }

                // READ PHASE: Get the message
                Message message;
                lock (_topic.Messages)
                {
                    message = _topic.Messages[curOffset];
                }

                // CONSUME PHASE: Process the message
                _topicSubscriber.Subscriber.Consume(message);

                // ADVANCE PHASE: Move to next message
                _topicSubscriber.Offset++;
            }
            catch (ThreadInterruptedException)
            {
                break;  // Exit the loop if interrupted
            }
        }
    }
}
```

---

## Step-by-Step Walkthrough with Example

### Initial Setup

```
Topic: "orders"
Messages: []  (empty initially)
Subscriber: S1
Offset: 0
```

### Step 1: Worker Thread Starts

```csharp
lock (_topicSubscriber)  // 🔒 Acquire lock on S1's TopicSubscriber object
{
    while (true)  // Start infinite loop
    {
```

**What happens:**
- Worker thread acquires exclusive lock on the `TopicSubscriber` object
- This prevents race conditions when reading/updating the offset
- The lock is held for the entire duration of the loop

---

### Step 2: Read Current Offset

```csharp
int curOffset = _topicSubscriber.Offset;  // curOffset = 0
```

**State:**
```
curOffset = 0
Messages.Count = 0
```

---

### Step 3: Check if Messages Available (Inner While Loop)

```csharp
while (curOffset >= _topic.Messages.Count)  // 0 >= 0 → TRUE
{
    Monitor.Wait(_topicSubscriber);  // 😴 SLEEP HERE
    curOffset = _topicSubscriber.Offset;
}
```

**What happens:**
- Condition: `0 >= 0` is TRUE (no messages available)
- Worker calls `Monitor.Wait(_topicSubscriber)`
- **CRITICAL:** This releases the lock and puts thread to sleep
- Worker is now BLOCKED, waiting to be woken up

**Thread State:**
```
Worker Thread: SLEEPING 😴
Lock: RELEASED (other threads can now acquire it)
Waiting for: Monitor.Pulse() call
```

---

### Step 4: Publisher Sends First Message

Meanwhile, in another thread:

```csharp
// Publisher thread
topic.AddMessage(new Message("order-1"));
```

**State after publish:**
```
Messages: ["order-1"]
Messages.Count = 1
S1.Offset = 0  (unchanged)
```

---

### Step 5: Worker Gets Woken Up

```csharp
// In TopicHandler.StartSubscriberWorker()
worker.WakeUpIfNeeded();

// Which calls:
lock (_topicSubscriber)
{
    Monitor.Pulse(_topicSubscriber);  // 📢 WAKE UP!
}
```

**What happens:**
- `Monitor.Pulse()` wakes up the sleeping worker
- Worker re-acquires the lock on `_topicSubscriber`
- Worker continues execution after `Monitor.Wait()`

**Worker resumes:**
```csharp
Monitor.Wait(_topicSubscriber);  // ← Returns from here
curOffset = _topicSubscriber.Offset;  // Re-read offset: curOffset = 0
```

---

### Step 6: Re-check Condition

```csharp
while (curOffset >= _topic.Messages.Count)  // 0 >= 1 → FALSE
{
    // Skip this block
}
```

**What happens:**
- Condition is now FALSE (we have messages!)
- Exit the inner while loop
- Proceed to message consumption

---

### Step 7: Read Message Safely

```csharp
Message message;
lock (_topic.Messages)  // 🔒 Lock the message list
{
    message = _topic.Messages[curOffset];  // Get message at index 0
}
```

**What happens:**
- Acquire lock on the message list (thread-safe read)
- Read message at index 0: `"order-1"`
- Release lock on message list

**State:**
```
message = Message("order-1")
curOffset = 0
S1.Offset = 0  (not yet updated)
```

---

### Step 8: Consume Message

```csharp
_topicSubscriber.Subscriber.Consume(message);
```

**What happens:**
- Call the subscriber's consume method
- This might take time (e.g., SleepingSubscriber sleeps for 1 second)
- **IMPORTANT:** Lock on `_topicSubscriber` is still held during consumption

**Output:**
```
Subscriber: S1 started consuming: order-1
[... 1 second delay ...]
Subscriber: S1 done consuming: order-1
```

---

### Step 9: Advance Offset

```csharp
_topicSubscriber.Offset++;  // Offset: 0 → 1
```

**State:**
```
S1.Offset = 1
Messages: ["order-1"]
Messages.Count = 1
```

---

### Step 10: Loop Back to Beginning

```csharp
}  // End of try block
catch (ThreadInterruptedException) { break; }
}  // End of outer while(true)
```

**What happens:**
- Loop back to the start of `while (true)`
- Read offset again: `curOffset = 1`

---

### Step 11: Check for More Messages

```csharp
int curOffset = _topicSubscriber.Offset;  // curOffset = 1

while (curOffset >= _topic.Messages.Count)  // 1 >= 1 → TRUE
{
    Monitor.Wait(_topicSubscriber);  // 😴 SLEEP AGAIN
    curOffset = _topicSubscriber.Offset;
}
```

**What happens:**
- No more messages available (offset 1, but only 1 message exists)
- Worker goes back to sleep
- Waiting for next message...

---

## Complete Example: Multiple Messages

Let's trace through a complete scenario:

### Initial State
```
Messages: []
S1.Offset: 0
Worker: SLEEPING 😴
```

### Publisher sends 3 messages rapidly

```csharp
topic.AddMessage(new Message("order-1"));
topic.AddMessage(new Message("order-2"));
topic.AddMessage(new Message("order-3"));
```

**State:**
```
Messages: ["order-1", "order-2", "order-3"]
Messages.Count: 3
S1.Offset: 0
```

### Worker wakes up and processes

#### Iteration 1:
```
curOffset = 0
Check: 0 >= 3? NO → Process
Read: Messages[0] = "order-1"
Consume: "order-1"
Offset: 0 → 1
```

#### Iteration 2:
```
curOffset = 1
Check: 1 >= 3? NO → Process
Read: Messages[1] = "order-2"
Consume: "order-2"
Offset: 1 → 2
```

#### Iteration 3:
```
curOffset = 2
Check: 2 >= 3? NO → Process
Read: Messages[2] = "order-3"
Consume: "order-3"
Offset: 2 → 3
```

#### Iteration 4:
```
curOffset = 3
Check: 3 >= 3? YES → SLEEP 😴
```

---

## Key Concepts Explained

### 1. Why Two While Loops?

```csharp
while (true)                              // Outer: Keep worker alive forever
{
    while (curOffset >= Messages.Count)   // Inner: Wait for new messages
    {
        Monitor.Wait();
    }
    // Process message
}
```

- **Outer loop:** Worker runs continuously (never exits)
- **Inner loop:** Handles waiting when no messages available

### 2. Monitor.Wait() Magic

```csharp
lock (_topicSubscriber)
{
    Monitor.Wait(_topicSubscriber);  // What happens here?
}
```

**Three things happen atomically:**
1. **Release the lock** on `_topicSubscriber`
2. **Put thread to sleep** (no CPU usage)
3. **Wait for notification** (Monitor.Pulse)

**When woken up:**
1. **Re-acquire the lock** on `_topicSubscriber`
2. **Continue execution** after the Wait() call

### 3. Why Re-read Offset After Wait?

```csharp
while (curOffset >= _topic.Messages.Count)
{
    Monitor.Wait(_topicSubscriber);
    curOffset = _topicSubscriber.Offset;  // ← Why this?
}
```

**Reason:** Offset might have been reset while sleeping!

**Example scenario:**
```csharp
// Worker is sleeping at offset 5
S1.Offset = 5

// User resets offset
broker.ResetOffset(topic, s1, 0);  // S1.Offset = 0

// Worker wakes up
curOffset = _topicSubscriber.Offset;  // Re-read: curOffset = 0
```

Without re-reading, worker would use stale offset value!

### 4. Lock Scope

```csharp
lock (_topicSubscriber)  // 🔒 Lock held for ENTIRE loop
{
    while (true)
    {
        // Read offset
        // Wait (releases lock temporarily)
        // Read message
        // Consume message  ← Lock still held here!
        // Update offset
    }
}
```

**Why hold lock during consumption?**
- Prevents offset from being modified during processing
- Ensures atomic read-consume-update operation
- Prevents race conditions with offset reset

---

## Race Condition Example (Without Proper Locking)

### Bad Implementation (No Lock):
```csharp
// ❌ WRONG - Race condition!
int offset = _topicSubscriber.Offset;  // Read: 5
Message msg = _topic.Messages[offset];  // Read message at 5

// ⚠️ Another thread resets offset to 0 here!

_topicSubscriber.Subscriber.Consume(msg);  // Consume message 5
_topicSubscriber.Offset++;  // Set offset to 6 (WRONG! Should be 1)
```

### Good Implementation (With Lock):
```csharp
// ✅ CORRECT - No race condition
lock (_topicSubscriber)
{
    int offset = _topicSubscriber.Offset;  // Read: 5
    Message msg = _topic.Messages[offset];  // Read message at 5
    
    // ✓ Lock prevents other threads from resetting offset
    
    _topicSubscriber.Subscriber.Consume(msg);  // Consume message 5
    _topicSubscriber.Offset++;  // Set offset to 6 (CORRECT)
}
```

---

## Visual Timeline

```
Time  | Worker Thread              | Publisher Thread        | State
------|----------------------------|-------------------------|------------------
T0    | Start, acquire lock        |                         | Offset=0, Msgs=0
T1    | Check: 0>=0? YES          |                         |
T2    | Monitor.Wait() 😴          |                         | Lock released
T3    | [SLEEPING]                 | Publish "order-1"       | Msgs=1
T4    | [SLEEPING]                 | Monitor.Pulse() 📢      |
T5    | Wake up, re-acquire lock   |                         |
T6    | Re-read offset: 0          |                         |
T7    | Check: 0>=1? NO           |                         |
T8    | Read Messages[0]           |                         |
T9    | Consume "order-1"          |                         | Processing...
T10   | Offset++                   |                         | Offset=1
T11   | Loop back                  |                         |
T12   | Check: 1>=1? YES          |                         |
T13   | Monitor.Wait() 😴          |                         | Lock released
T14   | [SLEEPING]                 | Publish "order-2"       | Msgs=2
T15   | [SLEEPING]                 | Monitor.Pulse() 📢      |
T16   | Wake up...                 |                         | Repeat cycle
```

---

## Common Questions

### Q1: Why not just use a simple if instead of while?

```csharp
// ❌ BAD
if (curOffset >= _topic.Messages.Count)
{
    Monitor.Wait(_topicSubscriber);
}

// ✅ GOOD
while (curOffset >= _topic.Messages.Count)
{
    Monitor.Wait(_topicSubscriber);
}
```

**Answer:** Spurious wakeups!
- `Monitor.Wait()` can wake up without `Monitor.Pulse()` being called
- Using `while` ensures we re-check the condition after waking up
- This is a standard pattern in concurrent programming

### Q2: What if multiple messages arrive while worker is consuming?

**Answer:** They queue up in the message log!

```
Worker consuming message 0 (takes 2 seconds)
Meanwhile:
  - Message 1 arrives
  - Message 2 arrives
  - Message 3 arrives

Worker finishes message 0, then processes 1, 2, 3 in sequence
```

### Q3: Can the worker process messages out of order?

**Answer:** No! The offset ensures sequential processing.

```
Offset always increments: 0 → 1 → 2 → 3 → ...
Messages always read in order: [0], [1], [2], [3], ...
```

---

## Summary

The `Run()` method implements a **producer-consumer pattern** with:

1. **Infinite loop:** Worker runs forever
2. **Wait mechanism:** Efficient sleeping when no work
3. **Offset tracking:** Sequential message processing
4. **Thread safety:** Locks prevent race conditions
5. **Wake-up protocol:** Monitor.Pulse() for notifications

This design is the foundation of systems like Kafka, RabbitMQ, and other message brokers! 🎉
