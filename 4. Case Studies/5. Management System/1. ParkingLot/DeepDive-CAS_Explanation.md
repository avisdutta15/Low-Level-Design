# CAS (Compare-And-Swap) Explanation

## Can we remove the loop in `TryOccupy`?

Yes, exactly. The loop can be removed because:

**The state is binary — `0` or `1`. There are only 2 possible outcomes:**

```csharp
int current = Volatile.Read(ref _occupied);
if (current == 1) return false;  // definitively occupied

if (Interlocked.CompareExchange(ref _occupied, value: 1, comparand: 0) == 0)
    return true;  // we won
// CAS failed -> _occupied must be 1 now
return false;  // same outcome as retrying
```

**Why the loop adds no value here:**

If CAS fails, it means another thread set `_occupied` to `1` in the tiny window between our `Read` and `CompareExchange`. On retry:
- `Volatile.Read` returns `1`
- We hit `if (current == 1) return false`

So the loop **always terminates in at most 2 iterations** and the second iteration always returns `false` — meaning we could just return `false` directly after the failed CAS instead of looping.

**A CAS loop is genuinely useful when:**
- The new value depends on the current value (e.g. incrementing a counter: `Read → compute newValue = current + 1 → CAS(current, newValue) → retry if stale`)
- There are more than 2 states and a failed CAS doesn't conclusively tell you the final answer

In our case, a failed CAS conclusively tells us the spot is occupied — no retry needed.

---

## Detailed Example Where the Loop Is Useful

A **concurrent counter** is the perfect example — the new value depends on the current value, so a failed CAS means the base you computed from is stale and you must retry with a fresh read.

```csharp
public class ConcurrentCounter
{
    private int _count = 0;

    public int Increment()
    {
        while (true)
        {
            int current = Volatile.Read(ref _count);  // Step 1: read current value
            int newValue = current + 1;               // Step 2: compute new value based on current

            // Step 3: CAS — set to newValue ONLY IF _count is still == current
            // If another thread changed _count between Step 1 and here, CAS fails -> retry
            if (Interlocked.CompareExchange(ref _count, value: newValue, comparand: current) == current)
                return newValue; // we won, _count is now current + 1

            // CAS failed: _count was changed by another thread between Read and CompareExchange
            // newValue is now stale (based on an old current), MUST retry with fresh read
        }
    }
}
```

---

### Why the loop is essential here — the race:

```
_count = 5

Thread A: Read  -> current = 5, newValue = 6
Thread B: Read  -> current = 5, newValue = 6
Thread C: Read  -> current = 5, newValue = 6

Thread A: CAS(5 -> 6) -> returns 5 ✅  _count = 6
Thread B: CAS(5 -> 6) -> returns 6 ❌  _count already 6, CAS fails
Thread C: CAS(5 -> 6) -> returns 6 ❌  _count already 6, CAS fails

Thread B: retry -> Read -> current = 6, newValue = 7
Thread B: CAS(6 -> 7) -> returns 6 ✅  _count = 7

Thread C: retry -> Read -> current = 7, newValue = 8
Thread C: CAS(7 -> 8) -> returns 7 ✅  _count = 8
```

Final `_count = 8` ✅ — all 3 increments applied correctly.

---

### Without the loop — what goes wrong:

```
_count = 5

Thread A: Read -> current = 5, newValue = 6
Thread B: Read -> current = 5, newValue = 6
Thread C: Read -> current = 5, newValue = 6

Thread A: CAS(5 -> 6) ✅  _count = 6
Thread B: CAS(5 -> 6) ❌  returns false, NO RETRY -> increment lost
Thread C: CAS(5 -> 6) ❌  returns false, NO RETRY -> increment lost
```

Final `_count = 6` ❌ — 2 increments silently lost.

---

### Contrast with `ParkingSpot.TryOccupy`:

| | `TryOccupy` | `ConcurrentCounter.Increment` |
|---|---|---|
| New value depends on current? | No — always `1` | Yes — always `current + 1` |
| Failed CAS tells you the answer? | Yes — spot is occupied, return `false` | No — just means value changed, must retry |
| Loop needed? | No | Yes |

This is the key distinction — **whenever `newValue = f(current)`**, a failed CAS means your `current` is stale, your `newValue` is wrong, and you must loop to recompute both.
