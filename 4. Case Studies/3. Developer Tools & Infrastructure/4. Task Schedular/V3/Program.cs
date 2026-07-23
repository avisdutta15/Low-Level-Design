// ── Demo ─────────────────────────────────────────────────────────────────────
// V3 — Multi-threaded Task Scheduler
// New: worker thread pool, CAS status transitions, concurrent data structures,
//      separate scheduler loop thread, graceful shutdown

using System.Collections.Concurrent;    //new — concurrent collections
using System.Collections.Immutable;     //new — ImmutableHashSet for observers
using V3;

//new — configurable worker pool size; scheduler starts immediately (no Run() needed)
var scheduler = new Scheduler(workerCount: 4);
scheduler.Subscribe(new ConsoleObserver());

Console.WriteLine("=== V3: Multi-threaded Scheduler ===\n");

// --- Scenario 1: Diamond DAG  A → B, A → C, B+C → D
// B and C run IN PARALLEL on different worker threads after A completes
scheduler.ScheduleTask("A", "Task-A", () =>
{
    Thread.Sleep(200);
    Console.WriteLine("  → A done");
}, DateTime.UtcNow);

scheduler.SubmitWithDependencies("B", "Task-B", () =>
{
    Thread.Sleep(300);
    Console.WriteLine("  → B done");
}, new() { "A" });

scheduler.SubmitWithDependencies("C", "Task-C", () =>
{
    Thread.Sleep(100);
    Console.WriteLine("  → C done");
}, new() { "A" });

// D waits on both B and C; Interlocked.Decrement ensures correctness
// even if B and C complete simultaneously on different threads
scheduler.SubmitWithDependencies("D", "Task-D", () =>
    Console.WriteLine("  → D done (after B and C)"), new() { "B", "C" });

// --- Scenario 2: Failure propagation (same as V2 but thread-safe via CAS)
scheduler.ScheduleTask("fail", "Failing Task",
    () => throw new Exception("Boom!"), DateTime.UtcNow.AddSeconds(1));
scheduler.SubmitWithDependencies("dep1", "Dep-1",
    () => Console.WriteLine("  → dep1 (should not run)"), new() { "fail" });
scheduler.SubmitWithDependencies("dep2", "Dep-2",
    () => Console.WriteLine("  → dep2 (should not run)"), new() { "dep1" });

// --- Scenario 3: Recurring task (creates new instance per recurrence)
scheduler.ScheduleRecurring("hb", "Heartbeat",
    () => Console.WriteLine("  → ♥ ping"), TimeSpan.FromSeconds(2));

// --- Scenario 4: Cancellation via CAS
scheduler.ScheduleTask("cancel-me", "Cancelled Task",
    () => Console.WriteLine("  → should not run"), DateTime.UtcNow.AddSeconds(5));
Task.Delay(500).ContinueWith(_ => scheduler.CancelTask("cancel-me"));

//new — no scheduler.Run() needed; scheduler starts workers+loop in constructor
// Shutdown after 8 seconds — graceful: drains queue, joins workers
Task.Delay(8000).ContinueWith(_ =>
{
    Console.WriteLine("\n=== Initiating shutdown... ===");
    scheduler.Shutdown();
});

Thread.Sleep(9000);
Console.WriteLine("\n=== Shutdown complete ===");

// ─────────────────────────────────────────────────────────────────────────────

namespace V3
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    //new — added "Scheduled" state between Pending and Running
    // Pending → Scheduled (enqueued) → Running (worker picked up) → Completed/Failed
    enum TaskStatus { Pending, Scheduled, Running, Completed, Failed, Cancelled }

    // Event types for observer notifications
    enum EventType  { Started, Completed, Failed, Cancelled }

    // ── Models ────────────────────────────────────────────────────────────────

    class ScheduledTask
    {
        //new — int backing field for CAS-based atomic transitions
        private int _status = (int)TaskStatus.Pending;

        public string    Id                 { get; }            // Unique identifier
        public string    Name               { get; }            // Human-readable name
        public Action    Action             { get; }            // The delegate to execute
        public DateTime  ScheduledTime      { get; }            //new — now immutable (no setter); recurring creates new instances
        public TimeSpan? RecurrenceInterval { get; }            // Repeat interval
        public bool      IsRecurring        => RecurrenceInterval.HasValue;
        public List<string> DependencyIds   { get; }            // Tasks this depends on
        //new — per-task cancellation token for cooperative cancellation
        public CancellationTokenSource Cts  { get; } = new();

        //new — public field (not property) so Interlocked.Decrement can take a ref
        // Kahn's in-degree: number of unfinished dependencies
        public int RemainingDeps;

        //new — thread-safe status read via Volatile.Read (ensures visibility across threads)
        public TaskStatus Status => (TaskStatus)Volatile.Read(ref _status);

        //new — CAS (Compare-And-Swap): atomically transitions state only if current == from
        // Returns true if THIS thread won the race. Prevents double-execution.
        public bool TryTransition(TaskStatus from, TaskStatus to)
            => Interlocked.CompareExchange(ref _status, (int)to, (int)from) == (int)from;

        public ScheduledTask(string id, string name, Action action,
            DateTime? scheduledTime = null, TimeSpan? recurrenceInterval = null,
            List<string>? deps = null)
        {
            Id = id; Name = name; Action = action;
            ScheduledTime = scheduledTime ?? DateTime.UtcNow;
            RecurrenceInterval = recurrenceInterval;
            DependencyIds = deps ?? new();
        }
    }

    // Immutable event record for observer notifications
    record TaskEvent(string TaskId, string TaskName, EventType EventType,
        DateTime Timestamp, Exception? Exception = null);

    // ── Observer ──────────────────────────────────────────────────────────────

    // Contract for lifecycle notifications
    interface ITaskObserver { void OnEvent(TaskEvent e); }

    //new — thread-safe console observer using lock to prevent interleaved output
    class ConsoleObserver : ITaskObserver
    {
        private readonly object _consoleLock = new();  //new — prevents garbled multi-thread output
        public void OnEvent(TaskEvent e)
        {
            lock (_consoleLock)  //new — only one thread writes to console at a time
            {
                Console.ForegroundColor = e.EventType switch
                {
                    EventType.Started   => ConsoleColor.Cyan,
                    EventType.Completed => ConsoleColor.Green,
                    EventType.Failed    => ConsoleColor.Red,
                    EventType.Cancelled => ConsoleColor.Yellow,
                    _                   => ConsoleColor.White
                };
                //new — includes thread name so you can see which worker executed what
                var thread = Thread.CurrentThread.Name ?? "?";
                var msg = e.Exception != null ? $" — {e.Exception.Message}" : "";
                Console.WriteLine($"[{e.EventType,-10}] [{thread,-12}] {e.TaskName}{msg}");
                Console.ResetColor();
            }
        }
    }

    // ── Scheduler ─────────────────────────────────────────────────────────────

    class Scheduler
    {
        //new — ConcurrentDictionary for thread-safe task storage (was Dictionary)
        private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();

        //new — BlockingCollection: producer-consumer queue
        // Workers block on GetConsumingEnumerable (no busy-wait/polling)
        // SchedulerLoop + Register produce; WorkerLoop consumes
        private readonly BlockingCollection<ScheduledTask> _queue = new();

        //new — global CancellationTokenSource for coordinated shutdown
        private readonly CancellationTokenSource _cts = new();

        //new — ImmutableHashSet + ImmutableInterlocked for lock-free observer management
        private ImmutableHashSet<ITaskObserver> _observers = ImmutableHashSet<ITaskObserver>.Empty;

        //new — worker thread pool array
        private readonly Thread[] _workers;

        //new — Kahn's adjacency list protected by _graphLock (was unprotected)
        private readonly Dictionary<string, List<string>> _adjList = new();
        //new — lock protecting all graph mutations (multiple threads can trigger OnCompleted)
        private readonly object _graphLock = new();

        //new — constructor spawns worker threads and scheduler loop (replaces Run())
        // Scheduler is active immediately after construction
        public Scheduler(int workerCount = 4)
        {
            _workers = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                //new — each worker is a background thread consuming from _queue
                _workers[i] = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"Worker-{i}"
                };
                _workers[i].Start();
            }

            //new — dedicated thread for time-based polling (checks ScheduledTime)
            new Thread(SchedulerLoop) { IsBackground = true, Name = "SchedulerLoop" }.Start();
        }

        //new — lock-free observer subscription via ImmutableInterlocked
        // Safe to call from any thread; creates a new ImmutableHashSet with the addition
        public void Subscribe(ITaskObserver o) => ImmutableInterlocked.Update(ref _observers, set => set.Add(o));

        // Schedule a one-time task; routes through Register()
        public void ScheduleTask(string id, string name, Action action,
            DateTime at, List<string>? deps = null)
            => Register(new ScheduledTask(id, name, action, at, deps: deps));

        // Schedule a recurring task; routes through Register()
        public void ScheduleRecurring(string id, string name, Action action,
            TimeSpan interval, List<string>? deps = null)
            => Register(new ScheduledTask(id, name, action, DateTime.UtcNow, interval, deps));

        // Convenience: schedule at now with explicit dependencies
        public void SubmitWithDependencies(string id, string name, Action action, List<string> deps)
            => ScheduleTask(id, name, action, DateTime.UtcNow, deps);

        //new — CAS-based cancellation (was simple property set in V2)
        // Tries to transition from Pending OR Scheduled to Cancelled
        public bool CancelTask(string id)
        {
            if (!_tasks.TryGetValue(id, out var task)) return false;

            //new — CAS: only succeeds if task is still in a cancellable state
            if (task.TryTransition(TaskStatus.Pending,   TaskStatus.Cancelled) ||
                task.TryTransition(TaskStatus.Scheduled, TaskStatus.Cancelled))
            {
                task.Cts.Cancel();  //new — signal per-task cancellation token
                Notify(new TaskEvent(task.Id, task.Name, EventType.Cancelled, DateTime.UtcNow));
                PropagateFailed(id);  // Dependents can never run
                return true;
            }
            // Task already Running/Completed — too late to cancel
            return false;
        }

        //new — graceful shutdown (was just _running = false in V1/V2)
        public void Shutdown()
        {
            _queue.CompleteAdding();    //new — signals no more items will be added; workers will exit after draining
            _cts.Cancel();              //new — wakes SchedulerLoop and workers blocked on cancellation token
            foreach (var w in _workers)
                w.Join(TimeSpan.FromSeconds(10));   //new — wait for each worker to finish current task
        }

        // ── Kahn's Algorithm ──────────────────────────────────────────────────

        // Register(): Build dependency graph, compute in-degree, enqueue if ready.
        //
        // EXAMPLE — Registering Diamond (A→B, A→C, B+C→D):
        //   Register("A", deps=[]):
        //     _adjList["A"] = []
        //     inDegree = 0 → Enqueue(A) immediately
        //
        //   Register("B", deps=["A"]):
        //     _adjList["A"].Add("B") → ["B"]
        //     "A" not Completed → inDegree = 1
        //     B.RemainingDeps = 1 → NOT enqueued
        //
        //   Register("D", deps=["B","C"]):
        //     _adjList["B"].Add("D"), _adjList["C"].Add("D")
        //     Neither completed → inDegree = 2
        //     D.RemainingDeps = 2 → NOT enqueued
        //
        private void Register(ScheduledTask task)
        {
            //new — TryAdd for thread safety + duplicate detection (was _tasks[id] = task)
            if (!_tasks.TryAdd(task.Id, task))
                throw new InvalidOperationException($"Duplicate task id: {task.Id}");

            lock (_graphLock)  //new — lock protects all graph mutations
            {
                // Ensure this task has an adjacency entry
                if (!_adjList.ContainsKey(task.Id))
                    _adjList[task.Id] = new();

                int inDegree = 0;
                foreach (var depId in task.DependencyIds)
                {
                    // Ensure the dependency has an adjacency entry
                    if (!_adjList.ContainsKey(depId))
                        _adjList[depId] = new();

                    // Forward edge: when depId completes, notify task.Id
                    _adjList[depId].Add(task.Id);

                    // Only count deps that haven't already completed
                    if (!(_tasks.TryGetValue(depId, out var dep) &&
                          dep.Status == TaskStatus.Completed))
                        inDegree++;
                }

                //new — Interlocked.Exchange ensures other threads see the value immediately
                Interlocked.Exchange(ref task.RemainingDeps, inDegree);
            }

            //new — if inDegree == 0, enqueue immediately (don't wait for SchedulerLoop)
            if (task.RemainingDeps == 0)
                Enqueue(task);
        }

        // OnCompleted(): Kahn's step — atomically decrement dependents' in-degree.
        //
        // EXAMPLE — A completes in Diamond:
        //   _adjList["A"] = ["B", "C"]
        //   Interlocked.Decrement(B.RemainingDeps) → 1→0 → Enqueue(B)
        //   Interlocked.Decrement(C.RemainingDeps) → 1→0 → Enqueue(C)
        //   Both B and C enter queue → picked up by DIFFERENT workers → PARALLEL!
        //
        // EXAMPLE — B completes, D waits on B+C:
        //   Interlocked.Decrement(D.RemainingDeps) → 2→1 (not 0, don't enqueue)
        //   Later C completes on another thread:
        //   Interlocked.Decrement(D.RemainingDeps) → 1→0 → Enqueue(D)
        //   Atomic decrement prevents race where both threads think they made it 0.
        //
        private void OnCompleted(string taskId)
        {
            List<string> deps;
            lock (_graphLock)  //new — lock for reading graph safely
            {
                if (!_adjList.TryGetValue(taskId, out deps!)) return;
            }

            foreach (var depId in deps)
            {
                if (!_tasks.TryGetValue(depId, out var dep)) continue;

                //new — Interlocked.Decrement: atomic, safe when multiple deps complete concurrently
                // Returns the NEW value after decrement
                if (Interlocked.Decrement(ref dep.RemainingDeps) == 0)
                    Enqueue(dep);  //new — immediately enqueue when all deps satisfied
            }
        }

        // PropagateFailed(): BFS under lock, using CAS to fail each task exactly once.
        //
        // EXAMPLE — "fail" fails, chain: fail → dep1 → dep2
        //   lock(_graphLock):
        //     BFS Queue: ["fail"]
        //     Dequeue "fail" → _adjList["fail"]=["dep1"]
        //       dep1.TryTransition(Pending→Failed) → CAS wins → Notify, enqueue "dep1"
        //     Dequeue "dep1" → _adjList["dep1"]=["dep2"]
        //       dep2.TryTransition(Pending→Failed) → CAS wins → Notify, enqueue "dep2"
        //     Dequeue "dep2" → no dependents → done
        //   Result: dep1+dep2 Failed without executing.
        //
        //   If another thread already moved dep1 to Running, CAS(Pending→Failed) would
        //   FAIL, and dep1 keeps running (too late to cancel from here).
        //
        private void PropagateFailed(string taskId)
        {
            lock (_graphLock)  //new — lock protects BFS traversal of graph
            {
                var queue = new Queue<string>();
                queue.Enqueue(taskId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    // Get `current`'s forward edges — i.e., tasks that depend on it
                    // If current has no entry in _adjList, it's a leaf node with no dependents → skip
                    if (!_adjList.TryGetValue(current, out var deps)) continue;

                    foreach (var depId in deps)
                    {
                        // Look up the task object by ID from the concurrent registry
                        // Defensive: if the ID exists in the graph but not in _tasks, skip it
                        if (!_tasks.TryGetValue(depId, out var dep)) continue;

                        //new — CAS ensures each task is only failed once; handles both Pending and Scheduled
                        if (dep.TryTransition(TaskStatus.Pending,   TaskStatus.Failed) ||
                            dep.TryTransition(TaskStatus.Scheduled, TaskStatus.Failed))
                        {
                            Notify(new TaskEvent(dep.Id, dep.Name, EventType.Failed, DateTime.UtcNow,
                                new Exception("Dependency failed")));
                            queue.Enqueue(depId);  // Continue BFS to this task's dependents
                        }
                    }
                }
            }
        }

        //new — Enqueue: CAS Pending→Scheduled, then add to BlockingCollection
        // CAS prevents double-enqueue if SchedulerLoop and OnCompleted race
        private void Enqueue(ScheduledTask task)
        {
            if (task.TryTransition(TaskStatus.Pending, TaskStatus.Scheduled))
                _queue.TryAdd(task);
        }

        // ── Scheduler Loop ────────────────────────────────────────────────────

        //new — entire method: dedicated background thread replaces the inline Run() loop
        // Handles future-scheduled tasks whose ScheduledTime hasn't arrived yet
        // (Register enqueues immediately only if inDegree==0 AND time is ready)
        private void SchedulerLoop()
        {
            //new — WaitOne(100) = sleep 100ms OR wake immediately on cancellation
            while (!_cts.Token.WaitHandle.WaitOne(100))
            {
                foreach (var task in _tasks.Values)
                {
                    if (task.Status != TaskStatus.Pending) continue;   // Only poll Pending tasks
                    if (task.RemainingDeps > 0) continue;               // Still waiting on deps
                    if (DateTime.UtcNow < task.ScheduledTime) continue; // Time not yet arrived

                    // CAS inside Enqueue — safe if another thread already enqueued it
                    Enqueue(task);
                }
            }
        }

        // ── Worker Loop ───────────────────────────────────────────────────────

        //new — entire method: consumer loop running on each worker thread
        // Replaces the synchronous Execute() called from Run() in V1/V2
        //
        // Flow per task:
        //   1. Dequeue from BlockingCollection (blocks if empty)
        //   2. Check per-task cancellation token
        //   3. CAS: Scheduled → Running (only ONE worker wins)
        //   4. Execute action
        //   5. CAS: Running → Completed/Failed
        //   6. OnCompleted() or PropagateFailed()
        //   7. If recurring, create and Register new instance
        //
        private void WorkerLoop()
        {
            try
            {
                //new — GetConsumingEnumerable blocks until an item is available (no busy-wait)
                // Throws OperationCanceledException when _cts is cancelled
                foreach (var task in _queue.GetConsumingEnumerable(_cts.Token))
                {
                    //new — check per-task cancellation before executing
                    if (task.Cts.Token.IsCancellationRequested) continue;

                    //new — CAS: Scheduled → Running
                    // Only ONE worker thread wins this; losers skip (prevents double-execution)
                    if (!task.TryTransition(TaskStatus.Scheduled, TaskStatus.Running))
                        continue;

                    // Notify observers that task started
                    Notify(new TaskEvent(task.Id, task.Name, EventType.Started, DateTime.UtcNow));
                    try
                    {
                        task.Action();  // Execute the actual work on this worker thread

                        //new — CAS: Running → Completed (was direct property set)
                        task.TryTransition(TaskStatus.Running, TaskStatus.Completed);
                        Notify(new TaskEvent(task.Id, task.Name, EventType.Completed, DateTime.UtcNow));

                        // Kahn's step: unlock dependents
                        OnCompleted(task.Id);

                        //new — Recurring: create a NEW task instance (can't reuse — original is Completed)
                        if (task.IsRecurring && !task.Cts.Token.IsCancellationRequested)
                        {
                            var next = new ScheduledTask(
                                task.Id + "_" + DateTime.UtcNow.Ticks,  //new — unique ID per recurrence
                                task.Name, task.Action,
                                DateTime.UtcNow.Add(task.RecurrenceInterval!.Value),
                                task.RecurrenceInterval);
                            Register(next);  // Goes through normal graph registration
                        }
                    }
                    catch (Exception ex)
                    {
                        //new — CAS: Running → Failed (was direct property set)
                        task.TryTransition(TaskStatus.Running, TaskStatus.Failed);
                        Notify(new TaskEvent(task.Id, task.Name, EventType.Failed, DateTime.UtcNow, ex));
                        // BFS: mark all transitive dependents as Failed
                        PropagateFailed(task.Id);
                    }
                }
            }
            catch (OperationCanceledException) { /* shutdown signal — exit cleanly */ }
        }

        // Broadcast event to all observers; swallow observer exceptions
        private void Notify(TaskEvent e)
        {
            foreach (var o in _observers)
                try { o.OnEvent(e); } catch { /* observer errors must not crash scheduler */ }
        }
    }
}
