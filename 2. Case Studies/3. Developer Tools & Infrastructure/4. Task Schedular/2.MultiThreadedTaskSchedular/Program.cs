using _2.MultiThreadedTaskSchedular.Observers;
using TaskScheduler = _2.MultiThreadedTaskSchedular.Entities.TaskScheduler;

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

Thread.Sleep(6000);
taskScheduler.Shutdown();
