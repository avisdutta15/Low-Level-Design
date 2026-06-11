using _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Observers;
using TaskScheduler = _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Entities.TaskScheduler;

using var scheduler = new TaskScheduler(workerCount: 3);
scheduler.AddObservers(new ConsoleObserver());

// Independent tasks (no dependencies, enqueued immediately)
scheduler.Submit("t1", "FetchData", () =>
{
    Thread.Sleep(500);
    Console.WriteLine("FetchData done.");
});

scheduler.Submit("t2", "LoadConfig", () =>
{
    Thread.Sleep(300);
    Console.WriteLine("LoadConfig done.");
});

// t3 depends on t1 and t2 — won't run until both complete
scheduler.SubmitWithDeps("t3", "ProcessData", () =>
{
    Thread.Sleep(400);
    Console.WriteLine("ProcessData done.");
}, deps: ["t1", "t2"]);

// t4 depends on t3
scheduler.SubmitWithDeps("t4", "GenerateReport", () =>
{
    Thread.Sleep(200);
    Console.WriteLine("GenerateReport done.");
}, deps: ["t3"]);

// t5 depends on t4, with a 2s delay after dependencies are met
scheduler.SubmitWithDeps("t5", "SendEmail", () =>
{
    Console.WriteLine("SendEmail done.");
}, deps: ["t4"], runAt: DateTimeOffset.UtcNow.AddSeconds(2));

// Recurring task with no dependencies
scheduler.Submit("t6", "Heartbeat", () =>
{
    Console.WriteLine($"Heartbeat at {DateTime.UtcNow:HH:mm:ss}");
}, runAt: DateTimeOffset.UtcNow.AddSeconds(1), interval: TimeSpan.FromSeconds(3));

// t7 will fail, causing t8 to be propagated as failed
scheduler.Submit("t7", "FlakyTask", () =>
{
    throw new Exception("FlakyTask exploded!");
});

scheduler.SubmitWithDeps("t8", "DependsOnFlaky", () =>
{
    Console.WriteLine("This should never run.");
}, deps: ["t7"]);

Thread.Sleep(15000);
scheduler.Shutdown();
