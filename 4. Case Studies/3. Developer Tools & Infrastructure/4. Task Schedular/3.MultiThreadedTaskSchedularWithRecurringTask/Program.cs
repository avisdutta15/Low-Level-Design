using _3.MultiThreadedTaskSchedularWithRecurringTask.Observers;
using TaskScheduler = _3.MultiThreadedTaskSchedularWithRecurringTask.Entities.TaskScheduler;

TaskScheduler taskScheduler = new TaskScheduler(workerCount: 3);
taskScheduler.AddObservers(new ConsoleObserver());

taskScheduler.Submit("t1", "TaskA", () =>
{
    Thread.Sleep(100);
    Console.WriteLine("Task A completed.");
});

taskScheduler.Submit("t2", "TaskB", () =>
{
    Thread.Sleep(100);
    Console.WriteLine("Task B completed.");
});

taskScheduler.Submit("t3", "TaskC", () =>
{
    Thread.Sleep(100);
    throw new Exception("Task C Got Exception!");
    Console.WriteLine("Task C completed.");
});

taskScheduler.Submit("t4", "TaskD", () =>
{
    Thread.Sleep(100);
    Console.WriteLine("Task D completed.");
});

//Thread.Sleep(100);
//taskScheduler.Shutdown();

taskScheduler.Submit("t5", "TaskE", () =>
{
    Thread.Sleep(100);
    Console.WriteLine("Task E completed.");
});

// Scheduled job: runs once after 3 seconds
taskScheduler.Submit("t6", "ScheduledTaskF", () =>
{
    Console.WriteLine("Scheduled Task F executed (delayed by 3s).");
}, scheduledTime: DateTimeOffset.UtcNow.AddSeconds(3));

// Recurring job: runs every 2 seconds
taskScheduler.Submit("t7", "RecurringTaskG", () =>
{
    Console.WriteLine($"Recurring Task G executed at {DateTime.UtcNow:HH:mm:ss}");
}, scheduledTime: DateTimeOffset.UtcNow.AddSeconds(1), recurringInterval: TimeSpan.FromSeconds(2));

Thread.Sleep(10000);
taskScheduler.Shutdown();
