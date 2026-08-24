using NotificationPatternsV6.Observers;
using NotificationPatternsV6;

ParkingLot parkingLot = new();

IObserver consoleObserver = new ConsoleObserver();
IObserver dashboardObserver = new DashboardObserver();

parkingLot.Subscribe(consoleObserver);
parkingLot.Subscribe(dashboardObserver);


parkingLot.ParkCar("Toyota");
parkingLot.ParkCar("Maruti");
parkingLot.ParkCar("Hyundai");

// --- Thread-Safety Demo ---
// Thread A iterates _observers in NotifyObservers (via ParkCar)
// Thread B mutates _observers via Subscribe concurrently
// Expected: InvalidOperationException - "Collection was modified; enumeration operation may not execute"
Console.WriteLine("\n--- Thread-Safety Demo ---");
var unsafeLot = new ParkingLot();
for (int i = 0; i < 10; i++)
    unsafeLot.Subscribe(new ConsoleObserver());

try
{
    var t1 = Task.Run(() => { for (int i = 0; i < 10000; i++) unsafeLot.ParkCar($"{i}"); });
    var t2 = Task.Run(() => { for (int i = 0; i < 10000; i++) unsafeLot.Subscribe(new ConsoleObserver()); });
    Task.WaitAll(t1, t2);
    Console.WriteLine("No exception this run - race conditions are non-deterministic, re-run to reproduce.");
}
catch (AggregateException ae)
{
    Console.WriteLine($"[RACE CONDITION] {ae.InnerException!.GetType().Name}: {ae.InnerException.Message}");
}