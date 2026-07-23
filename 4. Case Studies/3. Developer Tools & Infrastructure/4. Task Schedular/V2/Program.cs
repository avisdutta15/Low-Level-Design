// ── Demo ─────────────────────────────────────────────────────────────────────
// V2 — Single-threaded Task Scheduler + Dependency Tracking (Kahn's Algorithm)
// New: tasks declare dependencies; Kahn's in-degree tracking; BFS failure propagation

using V2;

// Create the scheduler and attach a console observer
var scheduler = new Scheduler();
scheduler.Subscribe(new ConsoleObserver());

Console.WriteLine("=== V2: Single-threaded Scheduler + Dependencies ===\n");

// --- Scenario 1: Linear chain  build → test → deploy
// "build" has no deps (inDegree=0), "test" waits on "build", "deploy" waits on "test"
scheduler.ScheduleTask("build",  "Build",  () => Console.WriteLine("  → Building..."),  DateTime.UtcNow);
scheduler.SubmitWithDependencies("test",   "Test",   () => Console.WriteLine("  → Testing..."),   new() { "build" });   //new — submit with deps
scheduler.SubmitWithDependencies("deploy", "Deploy", () => Console.WriteLine("  → Deploying..."), new() { "test" });    //new — submit with deps

// --- Scenario 2: Diamond  A → B, A → C, B+C → D                //new — diamond dependency scenario
// D has inDegree=2, won't run until BOTH B and C complete
scheduler.ScheduleTask("A", "Task-A", () => Console.WriteLine("  → A"), DateTime.UtcNow.AddSeconds(1));
scheduler.SubmitWithDependencies("B", "Task-B", () => Console.WriteLine("  → B"), new() { "A" });       //new
scheduler.SubmitWithDependencies("C", "Task-C", () => Console.WriteLine("  → C"), new() { "A" });       //new
scheduler.SubmitWithDependencies("D", "Task-D", () => Console.WriteLine("  → D"), new() { "B", "C" }); //new — multi-dep

// --- Scenario 3: Failure propagation  fail → dep1 → dep2       //new — failure propagation scenario
// When "fail" throws, BFS marks dep1 and dep2 as Failed without executing them
scheduler.ScheduleTask("fail", "Failing Task",
    () => throw new Exception("Boom!"), DateTime.UtcNow.AddSeconds(2));
scheduler.SubmitWithDependencies("dep1", "Dep-1", () => Console.WriteLine("  → dep1"), new() { "fail" });  //new
scheduler.SubmitWithDependencies("dep2", "Dep-2", () => Console.WriteLine("  → dep2"), new() { "dep1" }); //new

// --- Scenario 4: Recurring (no deps — works same as V1)
scheduler.ScheduleRecurring("hb", "Heartbeat",
    () => Console.WriteLine("  → ♥ ping"), TimeSpan.FromSeconds(2));

// Shutdown after 8 seconds
Task.Delay(8000).ContinueWith(_ => scheduler.Shutdown());

// Run() blocks main thread, polling every 100ms
scheduler.Run();

Console.WriteLine("\n=== Shutdown complete ===");

// ─────────────────────────────────────────────────────────────────────────────

namespace V2
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    // Lifecycle states a task can be in
    enum TaskStatus { Pending, Running, Completed, Failed, Cancelled }

    // Types of events observers get notified about
    enum EventType  { Started, Completed, Failed, Cancelled }

    // ── Models ────────────────────────────────────────────────────────────────

    class ScheduledTask
    {
        public string     Id                 { get; }            // Unique identifier
        public string     Name               { get; }            // Human-readable name
        public Action     Action             { get; }            // The delegate to execute
        public DateTime   ScheduledTime      { get; set; }       // Earliest time to run (mutable for recurring)
        public TimeSpan?  RecurrenceInterval { get; }            // Repeat interval (null = one-time)
        public bool       IsRecurring        => RecurrenceInterval.HasValue;
        public TaskStatus Status             { get; set; } = TaskStatus.Pending;
        public List<string> DependencyIds    { get; }            //new — IDs of tasks this depends on

        //new — Kahn's in-degree counter: how many unfinished deps remain
        // When this reaches 0, the task is eligible for execution
        public int RemainingDeps { get; set; }

        public ScheduledTask(string id, string name, Action action,
            DateTime? scheduledTime = null, TimeSpan? recurrenceInterval = null,
            List<string>? deps = null)                            //new — optional deps parameter
        {
            Id = id; Name = name; Action = action;
            ScheduledTime = scheduledTime ?? DateTime.UtcNow;
            RecurrenceInterval = recurrenceInterval;
            DependencyIds = deps ?? new();                       //new — store dependencies (empty if none)
        }
    }

    // Immutable event record passed to observers
    record TaskEvent(string TaskId, string TaskName, EventType EventType,
        DateTime Timestamp, Exception? Exception = null);

    // ── Observer ──────────────────────────────────────────────────────────────

    // Contract for receiving lifecycle notifications
    interface ITaskObserver { void OnEvent(TaskEvent e); }

    // Prints color-coded events to console
    class ConsoleObserver : ITaskObserver
    {
        public void OnEvent(TaskEvent e)
        {
            Console.ForegroundColor = e.EventType switch
            {
                EventType.Started   => ConsoleColor.Cyan,
                EventType.Completed => ConsoleColor.Green,
                EventType.Failed    => ConsoleColor.Red,
                EventType.Cancelled => ConsoleColor.Yellow,
                _                   => ConsoleColor.White
            };
            var msg = e.Exception != null ? $" — {e.Exception.Message}" : "";
            Console.WriteLine($"[{e.EventType,-10}] {e.TaskName}{msg}");
            Console.ResetColor();
        }
    }

    // ── Scheduler ─────────────────────────────────────────────────────────────

    class Scheduler
    {
        // Task registry keyed by ID
        private readonly Dictionary<string, ScheduledTask> _tasks = new();
        // Registered lifecycle observers
        private readonly List<ITaskObserver> _observers = new();
        // Controls the polling loop
        private bool _running = true;

        //new — Kahn's: forward adjacency list
        // _adjList["build"] = ["test"] means "test" depends on "build"
        // When "build" completes, we decrement "test"'s inDegree
        private readonly Dictionary<string, List<string>> _adjList = new();

        // Register an observer for lifecycle events
        public void Subscribe(ITaskObserver o) => _observers.Add(o);

        // Schedule a one-time task; routes through Register() to build graph
        public void ScheduleTask(string id, string name, Action action,
            DateTime at, List<string>? deps = null)              //new — optional deps param
            => Register(new ScheduledTask(id, name, action, at, deps: deps));  //new — calls Register()

        // Schedule a recurring task; also routes through Register()
        public void ScheduleRecurring(string id, string name, Action action,
            TimeSpan interval, List<string>? deps = null)        //new — optional deps param
            => Register(new ScheduledTask(id, name, action, DateTime.UtcNow, interval, deps));

        //new — Convenience: schedule at now with explicit dependencies
        public void SubmitWithDependencies(string id, string name, Action action, List<string> deps)
            => ScheduleTask(id, name, action, DateTime.UtcNow, deps);

        // Cancel a pending task; now also propagates failure to dependents
        public bool CancelTask(string id)
        {
            if (!_tasks.TryGetValue(id, out var task)) return false;
            if (task.Status != TaskStatus.Pending) return false;

            task.Status = TaskStatus.Cancelled;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Cancelled, DateTime.UtcNow));
            PropagateFailed(id);    //new — cancelled task will never complete → fail its dependents
            return true;
        }

        // Single-threaded scheduler loop — polls every 100ms
        public void Run()
        {
            while (_running)
            {
                foreach (var task in _tasks.Values.ToList())
                {
                    // Skip non-pending tasks
                    if (task.Status != TaskStatus.Pending) continue;
                    // Skip tasks still waiting on dependencies (inDegree > 0)
                    if (task.RemainingDeps > 0) continue;           //new — dependency guard
                    // Skip tasks whose time hasn't arrived
                    if (DateTime.UtcNow < task.ScheduledTime) continue;

                    // All conditions met — execute
                    Execute(task);

                    // Reschedule recurring tasks after successful completion
                    if (task.IsRecurring && task.Status == TaskStatus.Completed)
                    {
                        task.ScheduledTime = DateTime.UtcNow.Add(task.RecurrenceInterval!.Value);
                        task.Status = TaskStatus.Pending;
                    }
                }

                Thread.Sleep(100);  // 100ms polling interval
            }
        }

        // Signal the loop to exit
        public void Shutdown() => _running = false;

        // ── Kahn's Algorithm ──────────────────────────────────────────────────  //new — entire section

        // Register(): Wire edges and compute initial in-degree for the task.
        //
        // EXAMPLE — Registering "test" with deps=["build"]:
        //   1. _tasks["test"] = task
        //   2. _adjList["test"] = []
        //   3. For dep "build":
        //        _adjList["build"].Add("test")  → forward edge: build→test
        //        "build" not yet completed → inDegree++
        //   4. test.RemainingDeps = 1
        //
        // EXAMPLE — Registering "D" with deps=["B","C"]:
        //   _adjList["B"].Add("D"), _adjList["C"].Add("D")
        //   D.RemainingDeps = 2 (waits on both B and C)
        //
        private void Register(ScheduledTask task)
        {
            _tasks[task.Id] = task;

            // Ensure this task has an entry in the adjacency list
            if (!_adjList.ContainsKey(task.Id))
                _adjList[task.Id] = new();

            int inDegree = 0;

            // For each dependency, create a forward edge and count unfinished ones
            // Inside Register(), iterating test.DependencyIds = ["build"]
            foreach (var depId in task.DependencyIds)       // depId = "build", task.Id = "test"
            {
                // Ensure the dependency has an adjacency entry too
                if (!_adjList.ContainsKey(depId))
                    _adjList[depId] = new();

                // Forward edge: when depId completes, it should notify task.Id
                _adjList[depId].Add(task.Id);               // _adjList["build"].Add("test")

                // Only count deps that haven't already completed
                if (!(_tasks.TryGetValue(depId, out var dep) && dep.Status == TaskStatus.Completed))
                    inDegree++;                             // test's inDegree becomes 1
            }

            task.RemainingDeps = inDegree;
        }

        // OnCompleted(): Kahn's step — decrement in-degree of all dependents.
        //
        // EXAMPLE — OnCompleted("A") in diamond A→B, A→C:
        //   _adjList["A"] = ["B", "C"]
        //   B.RemainingDeps-- → 1→0 (B is now ready!)
        //   C.RemainingDeps-- → 1→0 (C is now ready!)
        //   Next loop tick picks up both B and C.
        //
        // EXAMPLE — OnCompleted("B") when D depends on B and C:
        //   D.RemainingDeps-- → 2→1 (still waiting on C)
        //   Later OnCompleted("C") → D.RemainingDeps-- → 1→0 (D is ready!)
        //
        private void OnCompleted(string taskId)
        {
            // Get the list of tasks that depend on the completed task
            if (_adjList.TryGetValue(taskId, out var deps) == false)
                return;

            // Decrement each dependent's in-degree
            foreach (var depId in deps)
                if (_tasks.TryGetValue(depId, out var dep))
                    dep.RemainingDeps--;    // hits 0 → scheduler loop picks it up next cycle
        }

        // PropagateFailed(): BFS traversal marking all transitive dependents as Failed.
        //
        // EXAMPLE — "fail" fails, chain: fail → dep1 → dep2
        //   BFS Queue: ["fail"]
        //   Dequeue "fail" → _adjList["fail"]=["dep1"]
        //     dep1.Status = Failed, Notify(Failed), enqueue "dep1"
        //   Dequeue "dep1" → _adjList["dep1"]=["dep2"]
        //     dep2.Status = Failed, Notify(Failed), enqueue "dep2"
        //   Dequeue "dep2" → _adjList["dep2"]=[] → done
        //   Result: dep1 and dep2 both Failed without executing.
        //
        private void PropagateFailed(string taskId)
        {
            var queue = new Queue<string>();
            queue.Enqueue(taskId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                // Get the list of tasks that DEPEND on `current` (its forward edges)
                // If current has no entry in the graph, it has no dependents → nothing to fail
                if (!_adjList.TryGetValue(current, out var deps)) continue;

                foreach (var depId in deps)
                {
                    // Look up the actual task object by ID
                    // If the task doesn't exist in the registry (defensive), skip it
                    if (!_tasks.TryGetValue(depId, out var dep)) continue;
                    // Only propagate to tasks still waiting (Pending) — already-running/completed tasks are unaffected
                    if (dep.Status != TaskStatus.Pending) continue;

                    dep.Status = TaskStatus.Failed;
                    Notify(new TaskEvent(dep.Id, dep.Name, EventType.Failed, DateTime.UtcNow,
                        new Exception("Dependency failed")));
                    // Continue BFS — this task's dependents also need to be failed
                    queue.Enqueue(depId);
                }
            }
        }

        // ── Private ───────────────────────────────────────────────────────────

        // Execute(): Run the task's action, handle success/failure, and trigger
        // Kahn's OnCompleted or BFS PropagateFailed accordingly.
        //
        // EXAMPLE — Successful "build":
        //   Status: Pending → Running → Completed
        //   Then OnCompleted("build") decrements "test".RemainingDeps
        //
        // EXAMPLE — Failing "fail":
        //   Status: Pending → Running → Failed
        //   Then PropagateFailed("fail") BFS-marks dep1, dep2 as Failed
        //
        private void Execute(ScheduledTask task)
        {
            // Transition: Pending → Running
            task.Status = TaskStatus.Running;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Started, DateTime.UtcNow));
            try
            {
                task.Action();  // Run the actual work
                // Transition: Running → Completed
                task.Status = TaskStatus.Completed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Completed, DateTime.UtcNow));
                OnCompleted(task.Id);   //new — Kahn's: unlock dependents by decrementing their inDegree
            }
            catch (Exception ex)
            {
                // Transition: Running → Failed
                task.Status = TaskStatus.Failed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Failed, DateTime.UtcNow, ex));
                PropagateFailed(task.Id);  //new — BFS: fail all transitive dependents
            }
        }

        // Broadcast event to all observers; swallow observer exceptions
        private void Notify(TaskEvent e)
        {
            foreach (var o in _observers)
                try { o.OnEvent(e); } catch { /* observer errors must not crash scheduler */ }
        }
    }
}
