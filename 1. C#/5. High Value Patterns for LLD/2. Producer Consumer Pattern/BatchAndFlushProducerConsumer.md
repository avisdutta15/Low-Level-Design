# Producer-Consumer: Building a Batch + Flush Pipeline with BlockingCollection

---

## The Goal

Decouple the **producer** (application thread logging messages) from the **consumer** (background thread writing to appenders).  
The producer should return immediately. The consumer should process messages efficiently in batches.

```
Application Thread (Producer)          Background Thread (Consumer)
──────────────────────────────         ────────────────────────────
logger.Info("msg1")  ──────┐
logger.Info("msg2")  ──────┤──► BlockingCollection ──► Batch ──► FlushBatch()
logger.Info("msg3")  ──────┘
returns immediately
```

---

## BlockingCollection State Machine

`BlockingCollection<T>` has two independent boolean flags that combine to form its state.

### The Two Flags

**`IsAddingCompleted`**
- Set to `true` when `CompleteAdding()` is called
- Means: "no new items will ever be enqueued"
- If a producer calls `Add()` after this → throws `InvalidOperationException`

**`IsCompleted`**
- `true` only when BOTH:
  - `CompleteAdding()` has been called (`IsAddingCompleted == true`)
  - AND the queue is empty (all items consumed)
- This is the "fully done" signal — safe to exit the consumer loop

### State Transition Diagram

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

The gap between `IsAddingCompleted` and `IsCompleted` is the **draining window** —  
items still in the queue after `CompleteAdding()` was called but not yet consumed.  
This window is the source of the race condition fixed in `ProcessQueueBatchV3`.

---

## Enqueue — The Producer Side

```csharp
private void Enqueue(LogMessage message)
{
    if (!_queue.IsAddingCompleted)
        _queue.Add(message);
}
```

- Guard against enqueuing after `Dispose()` has been called
- `Add()` blocks if the queue is full (back-pressure) — the producer thread sleeps until space is available
- This is the **entire cost on the calling thread** — no I/O, no formatting, just a queue push

---

## ProcessQueueV1 — The Baseline

```csharp
public void ProcessQueueV1()
{
    foreach (var item in _queue.GetConsumingEnumerable())
        FlushOne(item);
}
```

`GetConsumingEnumerable()` is a blocking enumerator:
- Blocks the thread (no CPU burn) when the queue is empty, waiting for the next item
- Automatically exits the `foreach` after `CompleteAdding()` is called AND the queue is empty
- Internally handles the final drain — no race window, no manual drain needed

**Example — 3 messages arrive, then Dispose() is called:**
```
GetConsumingEnumerable() ← blocks, waiting...
    msg1 arrives → yields msg1 → FlushOne(msg1)
    msg2 arrives → yields msg2 → FlushOne(msg2)
    msg3 arrives → yields msg3 → FlushOne(msg3)
    queue empty  → blocks again, waiting...
    CompleteAdding() called
    IsCompleted = true → foreach exits cleanly
    ← no messages lost, no manual drain needed
```

**Problem:** yields one item at a time. No way to say "give me up to 10 items" or  
"unblock after 1 second even if nothing arrived". Batching is not possible here.

| | V1 |
|---|---|
| Busy spinning | ❌ |
| Batching | ❌ |
| Flush interval | ❌ |
| Manual drain | ❌ not needed |

---

## ProcessQueueV2 — Manual TryTake, No Timeout

```csharp
public void ProcessQueueV2()
{
    while (_queue.IsCompleted == false)
    {
        if (_queue.TryTake(out var item))
            FlushOne(item);
    }
}
```

`TryTake()` without a timeout is **non-blocking** — returns `false` immediately if the queue is empty.

**Example — queue is idle for 2 seconds:**
```
TryTake() → false (queue empty)
TryTake() → false (queue empty)
TryTake() → false (queue empty)
... repeats thousands of times per second ...
← thread burns 100% CPU doing nothing useful
```

**Example — message arrives:**
```
TryTake() → false
TryTake() → false
TryTake() → true, got msg1 → FlushOne(msg1)
TryTake() → false
TryTake() → false
...
```

**Problem:** Busy-wait. The thread never sleeps — it spins in a tight loop consuming  
100% CPU even when there is nothing to process.  
Also no manual drain → race window on Dispose().

| | V1 | V2 |
|---|---|---|
| Busy spinning | ❌ | ✅ introduced |
| Batching | ❌ | ❌ |
| Flush interval | ❌ | ❌ |
| Manual drain | ❌ not needed | ❌ missing |

---

## ProcessQueueBatchV1 — Add Batching, Still Busy Spinning

```csharp
public void ProcessQueueBatchV1()
{
    var batch = new List<LogMessage>();
    int batchSize = 10;

    while (_queue.IsCompleted == false)
    {
        while (batch.Count < batchSize && _queue.TryTake(out var message))
            batch.Add(message);

        FlushBatch(batch);
        batch.Clear();
    }
}
```

Batching is introduced — collect up to `batchSize` items, then flush them together.  
But `TryTake()` still has no timeout — still non-blocking.

**Example — only 3 messages in queue, batchSize = 10:**
```
outer loop iteration:
    TryTake() → true,  got msg1  → batch=[msg1]
    TryTake() → true,  got msg2  → batch=[msg1, msg2]
    TryTake() → true,  got msg3  → batch=[msg1, msg2, msg3]
    TryTake() → false  (queue empty, batch not full yet, inner loop exits)
FlushBatch([msg1, msg2, msg3])  ← partial batch flushed immediately ✅
batch.Clear()

outer loop iteration:
    TryTake() → false (queue empty)
    TryTake() → false (queue empty)
    ... spins thousands of times ...  ← 100% CPU burn ❌
```

**Problem:** The outer `while` loop spins when the queue is empty — same busy-wait as V2.

| | V1 | V2 | BatchV1 |
|---|---|---|---|
| Busy spinning | ❌ | ✅ | ✅ still |
| Batching | ❌ | ❌ | ✅ introduced |
| Flush interval | ❌ | ❌ | ❌ |
| Manual drain | ❌ not needed | ❌ missing | ❌ missing |

---

## ProcessQueueBatchV2 — Fix Busy Spin with Timeout on First Item

```csharp
public void ProcessQueueBatchV2()
{
    var batch = new List<LogMessage>();
    int batchSize = 10;
    int timeout = 1000;

    while (_queue.IsCompleted == false)
    {
        if (_queue.TryTake(out var first, timeout))
        {
            batch.Add(first);

            while (batch.Count < batchSize && _queue.TryTake(out var next))
                batch.Add(next);
        }

        FlushBatch(batch);
        batch.Clear();
    }
}
```

Key insight: **block only for the first item, greedily grab the rest without blocking.**

`TryTake(out first, timeout)`:
- Parks the thread (OS scheduler removes it from CPU) until either:
  - A message arrives → wakes up immediately, returns `true`
  - `timeout` ms elapses with no message → returns `false`

Once we have the first item, the inner `TryTake()` (no timeout) grabs whatever is  
already sitting in the queue right now — no waiting for the batch to fill.

**Example — 3 messages arrive, batchSize = 10, timeout = 1000ms:**
```
TryTake(out first, 1000ms) ← thread parked, sleeping...
    msg1 arrives after 200ms → wakes up, returns true
    batch = [msg1]

    TryTake(out next) ← non-blocking, msg2 already in queue → true
    batch = [msg1, msg2]

    TryTake(out next) ← non-blocking, msg3 already in queue → true
    batch = [msg1, msg2, msg3]

    TryTake(out next) ← non-blocking, queue empty → false, inner loop exits

FlushBatch([msg1, msg2, msg3])  ← flushed after 200ms, not 1000ms ✅
batch.Clear()

TryTake(out first, 1000ms) ← thread parked again, sleeping...
    no messages for 1000ms → returns false
    batch stays empty
FlushBatch([])  ← no-op flush, harmless
```

**Why NOT block for the rest?**  
If we blocked on every item in the batch:
```
TryTake(out first,  1000ms) → got msg1  (waited up to 1000ms)
TryTake(out second, 1000ms) → got msg2  (waited up to 1000ms again)
TryTake(out third,  1000ms) → got msg3  (waited up to 1000ms again)
...
```
A batch of 10 could take up to **10 seconds** to assemble. The point of batching is  
to group messages that are **already there**, not to wait for more.

**Problem:** Race window on Dispose. After `CompleteAdding()` is called, `IsCompleted`  
becomes `true` and the loop exits. But a producer may have enqueued a message  
**after** the last `TryTake` returned `false` but **before** `CompleteAdding()` was called.  
That message is still in the queue and never gets flushed.

```
Consumer Thread                         Producer Thread
───────────────                         ───────────────
TryTake(out first, 1000ms)
  → queue empty, timeout elapses
  → returns false
  → FlushBatch([])
                                        _queue.Add(msg_last)  ← enqueued HERE

while (!_queue.IsCompleted)
  → IsCompleted = true (CompleteAdding just called)
  → loop exits

  ← msg_last is still in the queue, never flushed ❌
```

| | V1 | V2 | BatchV1 | BatchV2 |
|---|---|---|---|---|
| Busy spinning | ❌ | ✅ | ✅ | ❌ fixed |
| Batching | ❌ | ❌ | ✅ | ✅ |
| Flush interval | ❌ | ❌ | ❌ | ✅ introduced |
| Manual drain | ❌ not needed | ❌ missing | ❌ missing | ❌ still missing |

---

## ProcessQueueBatchV3 — Fix the Race Window with Manual Drain

```csharp
public void ProcessQueueBatchV3()
{
    var batch = new List<LogMessage>();
    int batchSize = 10;
    int timeout = 1000;

    while (_queue.IsCompleted == false)
    {
        if (_queue.TryTake(out var first, timeout))
        {
            batch.Add(first);
            while (batch.Count < batchSize && _queue.TryTake(out var next))
                batch.Add(next);
        }

        FlushBatch(batch);
        batch.Clear();
    }

    // Manual drain: catches messages in the race window between
    // the last TryTake returning false and CompleteAdding() being called.
    // GetConsumingEnumerable() (V1) handled this internally — here we own it.
    while (_queue.TryTake(out var remaining))
        batch.Add(remaining);

    FlushBatch(batch);
}
```

After the main loop exits, a final non-blocking `TryTake` drain empties whatever  
is left in the queue before the thread exits.

**Full Dispose() sequence:**
```
Main Thread (Dispose)                   Consumer Thread
─────────────────────                   ───────────────
                                        TryTake(out first, 1000ms)
                                          → queue empty, sleeping...

_queue.CompleteAdding()
  → IsAddingCompleted = true
  → TryTake wakes up, returns false
  → FlushBatch([])
  → batch.Clear()

  → while(!_queue.IsCompleted)
      IsCompleted = true → loop exits

                                        Manual drain:
                                        TryTake(out remaining) → msg_last ✅
                                        TryTake(out remaining) → false (empty)
                                        FlushBatch([msg_last])
                                        ← thread exits ProcessQueueBatchV3()

_consumerThread.Join()
  → blocks until consumer thread exits
  → all messages guaranteed flushed ✅

_queue.Dispose()
```

| | V1 | V2 | BatchV1 | BatchV2 | BatchV3 |
|---|---|---|---|---|---|
| Busy spinning | ❌ | ✅ | ✅ | ❌ | ❌ |
| Batching | ❌ | ❌ | ✅ | ✅ | ✅ |
| Flush interval | ❌ | ❌ | ❌ | ✅ | ✅ |
| Manual drain | ❌ not needed | ❌ missing | ❌ missing | ❌ missing | ✅ fixed |

---

## Why V1 Doesn't Need a Manual Drain

`GetConsumingEnumerable()` exits the `foreach` only after `IsCompleted` is `true`,  
which requires the queue to be **both** marked complete AND empty.  
The drain is baked into the enumerable — it keeps yielding until the last item is consumed,  
then exits. The race window is closed inside `BlockingCollection` itself.

The moment you switch to manual `TryTake`, you take ownership of that drain.

---

## Summary

```
V1  GetConsumingEnumerable
      ↓ need batching
V2  TryTake (no timeout)
      ↓ busy spinning — fix with timeout on first item
BatchV1  TryTake (no timeout) + batch
      ↓ still busy spinning — fix with timeout on first item only
BatchV2  TryTake(timeout) for first + TryTake() greedy drain
      ↓ race window on Dispose — fix with manual drain
BatchV3  BatchV2 + manual drain after loop  ✅
```
