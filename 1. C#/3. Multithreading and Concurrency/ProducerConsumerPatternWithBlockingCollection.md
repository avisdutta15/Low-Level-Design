# Producer-Consumer Patterns with BlockingCollection\<T\>

## What is BlockingCollection\<T\>?

`BlockingCollection<T>` is a thread-safe collection in `System.Collections.Concurrent` designed specifically for producer-consumer scenarios. It wraps an underlying `IProducerConsumerCollection<T>` (default: `ConcurrentQueue<T>`) and adds two critical capabilities:

1. **Blocking** — consumers can block (sleep) until an item is available, instead of busy-spinning.
2. **Bounding** — the collection can be capped at a maximum size, causing producers to block when full (back-pressure).

### Basic Usage
```csharp
var collection = new BlockingCollection<int>(boundedCapacity: 100);

// Producer Thread
Task.Run(()=>{
    foreach(var i in GetItems())
        collection.Add(i);          // Blocks if Q is full!

    // Signal the consumer that no new item will be enqueued
    collection.CompleteAdding();
});

// Consumer Thread
Task.Run(()=>{
    foreach(var i in collection.GetConsumingEnumerable())       // Blocks if Q is empty!
        Console.WriteLine(i);
});
```

### State Management
`BlockingCollection<T>`` has two independent boolean flags that combine to form its state:
**IsAddingCompleted**
- Set to true when `CompleteAdding()` is called
- Means: "no new items will ever be enqueued"
- Producers check this before calling `Add()` — if true, adding throws `InvalidOperationException`

**IsCompleted**
- `true` only when BOTH conditions are met:
- `CompleteAdding()` has been called (`IsAddingCompleted == true`)
- AND the queue is empty (all items have been consumed)
This is the "fully done" signal for the consumer loop
```
                    CompleteAdding()
                         │
                         ▼
┌─────────────┐    ┌─────────────────────┐    ┌──────────────┐
│   Normal    │───►│  IsAddingCompleted  │───►│ IsCompleted  │
│             │    │  = true             │    │ = true       │
│ Adding: ✅  │    │                     │    │              │
│ Taking: ✅  │    │  Adding: ❌         │    │ Adding: ❌   │
│             │    │  Taking: ✅         │    │ Taking: ❌   │
└─────────────┘    └─────────────────────┘    └──────────────┘
                                                    ▲
                                          queue becomes empty
```
### Key Properties and Methods

```csharp
var queue = new BlockingCollection<string>(boundedCapacity: 100);

// State
queue.Count              // Current number of items
queue.BoundedCapacity    // Max capacity (int.MaxValue if unbounded)
queue.IsAddingCompleted  // True after CompleteAdding() is called
queue.IsCompleted        // True when IsAddingCompleted AND Count == 0

// Producer methods
queue.Add(item);                          // Blocks if full
queue.TryAdd(item);                       // Returns false if full (non-blocking)
queue.TryAdd(item, timeout);              // Blocks up to timeout if full
queue.TryAdd(item, timeout, token);       // Blocks up to timeout, respects cancellation

// Consumer methods
queue.Take();                             // Blocks if empty
queue.TryTake(out item);                  // Returns false if empty (non-blocking)
queue.TryTake(out item, timeout);         // Blocks up to timeout if empty
queue.TryTake(out item, timeout, token);  // Blocks up to timeout, respects cancellation

// Enumerable consumer
queue.GetConsumingEnumerable();            // Yields items, blocks when empty, ends on completion
queue.GetConsumingEnumerable(token);       // Same, but respects cancellation

// Lifecycle
queue.CompleteAdding();  // Signals no more items will be added
queue.Dispose();         // Releases resources
```

### Blocking Behaviour

In a typical Producer-Consumer pattern:
- **Consumers** use `Take` or `TryTake` to remove items.
- **Producers** use `Add` or `TryAdd` to insert items. 

**Why it blocks the Consumer**
When you call `TryTake(out item, timeout)` on an empty collection, the consumer thread is put into a "sleep" or wait state. It will stay blocked until: 
1. A Producer adds an item.
2. The timeout expires.
3. The collection is marked as completed. 

**When would a Producer block?**
The Producer thread only blocks if the `BlockingCollection` has a bounded capacity (a set limit) and is currently full. In that case: 
- Calling `Add()` will block the producer indefinitely until space is available.
- Calling `TryAdd(item, timeout)` will block the producer only until the timeout expires or space becomes available

### Bounded vs Unbounded

```csharp
// Unbounded — grows without limit (risk: OutOfMemoryException under load)
var unbounded = new BlockingCollection<string>();

// Bounded — blocks producers when full (back-pressure)
var bounded = new BlockingCollection<string>(boundedCapacity: 1000);
```

Always prefer bounded in production. Without a bound, a fast producer and slow consumer will eat memory until the process crashes.

### Underlying Collection

By default, `BlockingCollection<T>` uses a `ConcurrentQueue<T>` (FIFO). You can swap it:

```csharp
// FIFO (default) — messages processed in order
var fifo = new BlockingCollection<string>(new ConcurrentQueue<string>());

// LIFO — most recent items processed first
var lifo = new BlockingCollection<string>(new ConcurrentStack<string>());

// Unordered — thread-local storage, best for high-concurrency add/take
var unordered = new BlockingCollection<string>(new ConcurrentBag<string>());
```

---

## Pattern 1: TryAdd / TryTake with Manual Loop

This is the most flexible pattern. You control batching, timeouts, and shutdown behavior explicitly.

### Basic Example

```csharp
using System.Collections.Concurrent;

var queue = new BlockingCollection<string>(boundedCapacity: 100);
var cts = new CancellationTokenSource();

// Producer
var producer = Task.Run(() =>
{
    try
    {
        for (int i = 0; i < 50; i++)
        {
            // TryAdd with timeout + cancellation token
            // Returns false if queue is full and timeout expires
            if (!queue.TryAdd($"Message {i}", millisecondsTimeout: 5000, cts.Token))
            {
                Console.WriteLine($"Failed to enqueue message {i} (queue full or timeout)");
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Producer cancelled");
    }
    finally
    {
        // Signal that no more items will be added.
        // This is what allows the consumer to eventually exit.
        queue.CompleteAdding();
    }
});

// Consumer
var consumer = Task.Run(() =>
{
    try
    {
        while (!queue.IsCompleted)
        {
            // TryTake with timeout + cancellation token
            // Blocks the Consumer Thread up to 1 second waiting for an item
            if (queue.TryTake(out var item, millisecondsTimeout: 1000, cts.Token))
            {
                Console.WriteLine($"Consumed: {item}");
            }
            // else: timeout expired, loop back and check IsCompleted
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Consumer cancelled");
    }
});

// Wait for both to finish
Task.WaitAll(producer, consumer);

// Cleanup
queue.Dispose();
```

### How TryAdd / TryTake Behave

| Method | Queue State | Behavior |
|--------|------------|----------|
| `TryAdd(item)` | Not full | Adds immediately, returns `true` |
| `TryAdd(item)` | Full | Returns `false` immediately (non-blocking). Does not block Producer thread. |
| `TryAdd(item, timeout)` | Full | Blocks Producer thread up to `timeout`, returns `false` if still full |
| `TryAdd(item, timeout, token)` | Full | Same, but throws `OperationCanceledException` if token is cancelled |
| `TryTake(out item)` | Not empty | Takes immediately, returns `true` |
| `TryTake(out item)` | Empty | Returns `false` immediately (non-blocking). Does not block Consumer thread. |
| `TryTake(out item, timeout)` | Empty | Blocks Consumer thread up to `timeout`, returns `false` if still empty |
| `TryTake(out item, timeout, token)` | Empty | Same, but throws `OperationCanceledException` if token is cancelled |

### Cancellation Token Behavior

The `CancellationToken` interrupts the blocking wait. Without it, `TryTake(out item, Timeout.Infinite)` would block forever if the queue is empty and `CompleteAdding()` is never called.

```csharp
var cts = new CancellationTokenSource();

// Cancel after 10 seconds (e.g., application shutdown timeout)
cts.CancelAfter(TimeSpan.FromSeconds(10));

try
{
    // This will throw OperationCanceledException after 10 seconds
    // even if the queue is not empty and CompleteAdding was not called
    queue.TryTake(out var item, Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Graceful exit — drain remaining items if needed
    while (queue.TryTake(out var remaining))
        Console.WriteLine($"Draining: {remaining}");
}
```

### Disposal and Shutdown Sequence

The correct shutdown order matters:

```csharp
// Step 1: Signal no more items (unblocks consumers waiting on TryTake)
queue.CompleteAdding();

// Step 2: Wait for consumer to finish processing remaining items
await consumerTask;

// Step 3: Dispose the collection
queue.Dispose();
```

What happens if you get this wrong:

| Mistake | Result |
|---------|--------|
| `Dispose()` before `CompleteAdding()` | `ObjectDisposedException` in producer/consumer |
| `Dispose()` before consumer finishes | `ObjectDisposedException` during `TryTake` |
| Never call `CompleteAdding()` | Consumer blocks forever (unless using cancellation token) |
| `CompleteAdding()` then `Add()` | `InvalidOperationException` |

### Batching with TryTake

This is the pattern we use in our logging framework:

```csharp
var consumer = Task.Run(() =>
{
    var batch = new List<string>(batchSize: 10);

    while (!queue.IsCompleted)
    {
        batch.Clear();

        try
        {
            // Block until first item arrives (or timeout)
            if (queue.TryTake(out var first, millisecondsTimeout: 1000, cts.Token))
            {
                batch.Add(first);

                // Greedily drain more without blocking
                while (batch.Count < 10 && queue.TryTake(out var next))
                {
                    batch.Add(next);
                }
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }

        // Process entire batch at once
        foreach (var item in batch)
            Console.WriteLine($"Batch item: {item}");
    }

    // Final drain after CompleteAdding
    while (queue.TryTake(out var remaining))
        Console.WriteLine($"Draining: {remaining}");
});
```

---

## Pattern 2: GetConsumingEnumerable

The simplest pattern. `GetConsumingEnumerable()` returns an `IEnumerable<T>` that:
- Blocks Consumer threads when the queue is empty (waits for items)
- Yields items as they become available
- Exits the enumeration when `CompleteAdding()` is called and the queue is drained

### Basic Example

```csharp
using System.Collections.Concurrent;

var queue = new BlockingCollection<string>(boundedCapacity: 100);
var cts = new CancellationTokenSource();

// Producer
var producer = Task.Run(() =>
{
    try
    {
        for (int i = 0; i < 50; i++)
        {
            queue.Add($"Message {i}", cts.Token);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Producer cancelled");
    }
    finally
    {
        queue.CompleteAdding();
    }
});

// Consumer — entire loop in one line
var consumer = Task.Run(() =>
{
    try
    {
        foreach (var item in queue.GetConsumingEnumerable(cts.Token))
        {
            Console.WriteLine($"Consumed: {item}");
        }
        // Loop exits automatically when:
        //   - CompleteAdding() was called AND queue is empty
        //   - OR cancellation token is triggered
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Consumer cancelled");
    }
});

Task.WaitAll(producer, consumer);
queue.Dispose();
```

### How GetConsumingEnumerable Works Internally

Under the hood, `GetConsumingEnumerable` is roughly equivalent to:

```csharp
// Simplified pseudocode of what GetConsumingEnumerable does
IEnumerable<T> GetConsumingEnumerable(CancellationToken token)
{
    while (!IsCompleted)
    {
        T item;
        try
        {
            // Blocks indefinitely until an item is available
            // or CompleteAdding is called
            item = Take(token);  // throws if cancelled or completed
        }
        catch (InvalidOperationException)
        {
            // IsCompleted became true — exit
            yield break;
        }

        yield return item;  // Removes the item from the collection
    }
}
```

Key detail: each `yield return` *removes* the item from the collection. It's consuming, not peeking.

### Cancellation Token with GetConsumingEnumerable

Without a token, the enumerable blocks forever when the queue is empty. The token gives you an escape hatch:

```csharp
var cts = new CancellationTokenSource();

// Cancel after 30 seconds (e.g., graceful shutdown deadline)
cts.CancelAfter(TimeSpan.FromSeconds(30));

try
{
    foreach (var item in queue.GetConsumingEnumerable(cts.Token))
    {
        Process(item);
    }
}
catch (OperationCanceledException)
{
    // Token was cancelled — drain remaining items manually if needed
    while (queue.TryTake(out var remaining))
        Process(remaining);
}
```

### Disposal and Shutdown Sequence

Same as Pattern 1 — `CompleteAdding()` first, wait for consumer, then `Dispose()`:

```csharp
queue.CompleteAdding();    // Enumerable will finish yielding remaining items and exit
await consumerTask;        // Wait for foreach to complete
queue.Dispose();           // Safe to dispose now
```

### Limitation: No Batching

`GetConsumingEnumerable` yields one item at a time. If you need batching, you have to buffer manually, which defeats the simplicity:

```csharp
// Awkward batching with GetConsumingEnumerable
var batch = new List<string>(10);
foreach (var item in queue.GetConsumingEnumerable(cts.Token))
{
    batch.Add(item);
    if (batch.Count >= 10)
    {
        FlushBatch(batch);
        batch.Clear();
    }
}
// Problem: what about the last partial batch?
// Problem: no timeout-based flushing during idle periods
if (batch.Count > 0)
    FlushBatch(batch);
```

This works but loses the timeout-based flushing. During idle periods, a partial batch sits in memory until the next item arrives or `CompleteAdding()` is called. The manual `TryTake` loop (Pattern 1) handles this better.

---

## Pattern 3: Multiple Consumers (Competing Consumers)

Multiple consumer threads pull from the same queue. `BlockingCollection` handles the thread safety — each item is consumed by exactly one consumer.

```csharp
var queue = new BlockingCollection<string>(boundedCapacity: 1000);
var cts = new CancellationTokenSource();

// Producer
var producer = Task.Run(() =>
{
    for (int i = 0; i < 1000; i++)
        queue.Add($"Task {i}");
    queue.CompleteAdding();
});

// Multiple consumers — each processes different items
int consumerCount = Environment.ProcessorCount;
var consumers = Enumerable.Range(0, consumerCount).Select(id =>
    Task.Run(() =>
    {
        foreach (var item in queue.GetConsumingEnumerable(cts.Token))
        {
            Console.WriteLine($"Consumer {id}: {item}");
        }
    })
).ToArray();

await producer;
await Task.WhenAll(consumers);
queue.Dispose();
```

### When to Use

- CPU-bound processing where a single consumer can't keep up
- Work distribution (task queues, job processors)

### When NOT to Use

- When ordering matters (items are distributed across consumers non-deterministically)
- I/O-bound consumers writing to the same destination (they'll contend on the I/O lock)
- Logging — this is why our logging framework uses single-consumer-per-appender instead

---

## Pattern 4: Multiple Producers, Single Consumer

The most common pattern in logging. Many threads produce messages, one thread consumes them.

```csharp
var queue = new BlockingCollection<string>(boundedCapacity: 5000);

// Multiple producers (e.g., web request handler threads)
var producers = Enumerable.Range(0, 10).Select(id =>
    Task.Run(() =>
    {
        for (int i = 0; i < 100; i++)
            queue.Add($"Producer {id}: Message {i}");
    })
).ToArray();

// Single consumer
var consumer = Task.Run(() =>
{
    foreach (var item in queue.GetConsumingEnumerable())
        Console.WriteLine(item);
});

// Wait for all producers, then signal completion
await Task.WhenAll(producers);
queue.CompleteAdding();
await consumer;
queue.Dispose();
```

Note the shutdown sequence: you must wait for ALL producers to finish before calling `CompleteAdding()`. If you call it while a producer is still running, the producer's next `Add()` throws `InvalidOperationException`.

Here's the timeline:

1. All 10 producers run concurrently, adding 100 messages each (1,000 total).
2. While producers are adding, the consumer is already running — `GetConsumingEnumerable()` is yielding and processing items as they arrive.
3. `await Task.WhenAll(producers)` waits until all producers finish. By this point, some or all of the 1,000 messages may already be consumed.
4. `queue.CompleteAdding()` marks the queue as complete. This tells `GetConsumingEnumerable()`: *"after you drain whatever's left, you're done."*
5. The consumer's foreach continues yielding remaining items from the queue. Once the queue is empty AND `IsAddingCompleted` is true, `GetConsumingEnumerable()` exits the loop.
6. `await consumer` waits for that to finish.

The key thing: `CompleteAdding()` doesn't mean **"stop consuming."** It means **"stop accepting new items."** The `IsCompleted` property only becomes true when both conditions are met:

- `IsCompleted = IsAddingCompleted && Count == 0`
- So `GetConsumingEnumerable()` keeps yielding until the queue is fully drained. No messages are lost.

The only scenario where you'd lose messages is if you called `queue.Dispose()` before the consumer finishes — that would throw `ObjectDisposedException` inside the foreach. But in this code, await consumer ensures the consumer is done before `Dispose()` runs.

---

## Pattern 5: Pipeline (Chained Producer-Consumers)

Each stage is both a consumer of the previous stage and a producer for the next. Common in data processing pipelines.

```csharp
var raw = new BlockingCollection<string>(100);
var parsed = new BlockingCollection<LogEntry>(100);
var formatted = new BlockingCollection<string>(100);

// Stage 1: Read raw lines
var reader = Task.Run(() =>
{
    foreach (var line in File.ReadLines("app.log"))
        raw.Add(line);
    raw.CompleteAdding();
});

// Stage 2: Parse into structured objects
var parser = Task.Run(() =>
{
    foreach (var line in raw.GetConsumingEnumerable())
        parsed.Add(LogEntry.Parse(line));
    parsed.CompleteAdding();
});

// Stage 3: Format for output
var formatter = Task.Run(() =>
{
    foreach (var entry in parsed.GetConsumingEnumerable())
        formatted.Add(entry.ToJson());
    formatted.CompleteAdding();
});

// Stage 4: Write to destination
var writer = Task.Run(() =>
{
    foreach (var json in formatted.GetConsumingEnumerable())
        Console.WriteLine(json);
});

await Task.WhenAll(reader, parser, formatter, writer);

raw.Dispose();
parsed.Dispose();
formatted.Dispose();
```

Each `BlockingCollection` acts as a buffer between stages. If one stage is slower, its input queue fills up and back-pressure propagates upstream. The `CompleteAdding()` call cascades through the pipeline — when stage 1 finishes, it signals stage 2, which signals stage 3, and so on.

---

## Pattern 6: Priority Queue Consumer

Use `BlockingCollection` with a custom `IProducerConsumerCollection<T>` to process high-priority items first. .NET doesn't provide a concurrent priority queue out of the box, but you can build one:

```csharp
// Simplified approach: separate queues per priority
var highPriority = new BlockingCollection<string>(100);
var lowPriority = new BlockingCollection<string>(1000);

var consumer = Task.Run(() =>
{
    while (!highPriority.IsCompleted || !lowPriority.IsCompleted)
    {
        // Always drain high-priority first
        if (highPriority.TryTake(out var urgent, millisecondsTimeout: 0))
        {
            Console.WriteLine($"[URGENT] {urgent}");
            continue;
        }

        // Fall back to low-priority with a short timeout
        if (lowPriority.TryTake(out var normal, millisecondsTimeout: 100))
        {
            Console.WriteLine($"[NORMAL] {normal}");
        }
    }
});
```

Alternatively, use `BlockingCollection.TryTakeFromAny` to wait on multiple collections simultaneously:

```csharp
var queues = new[] { highPriority, lowPriority };

var consumer = Task.Run(() =>
{
    while (queues.Any(q => !q.IsCompleted))
    {
        // Takes from the first collection that has an item available
        // Checks collections in array order (index 0 first = high priority)
        int index = BlockingCollection<string>.TryTakeFromAny(
            queues, out var item, millisecondsTimeout: 1000);

        if (index >= 0)
            Console.WriteLine($"[Priority {index}] {item}");
    }
});
```

---

## Summary: Choosing the Right Pattern

| Pattern | Producers | Consumers | Ordering | Best For |
|---------|-----------|-----------|----------|----------|
| TryAdd/TryTake manual loop | Any | 1 | FIFO | Batching, timeout-based flushing (logging) |
| GetConsumingEnumerable | Any | 1 | FIFO | Simple consumers, low complexity |
| Competing consumers | Any | N | None | CPU-bound work distribution |
| Many producers, single consumer | N | 1 | FIFO | Logging, event aggregation |
| Pipeline | 1 per stage | 1 per stage | FIFO per stage | Multi-stage data processing |
| Priority queues | Any | 1 | Priority | Urgent vs normal processing |
