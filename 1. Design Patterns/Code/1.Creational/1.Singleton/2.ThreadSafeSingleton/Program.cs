namespace _2.ThreadSafeSingleton;

public class Singleton
{
    private static Singleton? _instance = null;
    private static int _instanceCount = 0;
    private static readonly object _lockObj = new object(); 
    public static int InstanceCount {get =>_instanceCount;}

    private Singleton()
    {
        _instanceCount++;
    }

    public static Singleton GetInstance()
    {
        if(_instance == null)
        {
            lock (_lockObj)
            {
                if(_instance == null)
                {
                    _instance = new Singleton();
                }
            }           
        }
        return _instance;
    }
    //Other methods
}

public class Program
{
    public static async Task Main(string [] args)
    {
        Tests.GetInstance_AlwaysReturnsSameInstance();     
        Console.WriteLine(); 
        await Tests.GetInstance_ConcurrentAccess_CreatesMoreThanOneInstance();
    }
}

public class Tests
{
    public static void GetInstance_AlwaysReturnsSameInstance()
    {
        Singleton instance1 = Singleton.GetInstance();
        Singleton instance2 = Singleton.GetInstance();

        // **Assertion 1: Check if the instances are not null**
        Console.WriteLine($"instance1!=null : {instance1!=null}");
        Console.WriteLine($"instance2!=null : {instance2!=null}");

        Console.WriteLine();

        // **Assertion 2: Check if both references point to the exact same object**
        Console.WriteLine($"instance1?.Equals(instance2) :{instance1?.Equals(instance2)}");
    }

    public static async Task GetInstance_ConcurrentAccess_CreatesMoreThanOneInstance()
    {
        //Create a list of tasks that returns SimpleSingleton.
        //Create a hashset to store unique instances from the list of instance.
        List<Task<Singleton>> taskList = new();
        HashSet<Singleton> uniqueueInstances = new();

        // Create an array of tasks, each task calls GetInstance()
        Parallel.For(0, 10_000, (_) =>
        {
            taskList.Add(Task.Run(() => 
            {
                return Singleton.GetInstance();
            }));
        });

        //Wait for all the taks to complete.
        await Task.WhenAll(taskList);

        //Collect the result i.e. the instances
        var instanceList = taskList.Select(task => task.Result).ToList();

        // Add all returned instances to a HashSet to count unique references
        foreach(var instance in instanceList)
        {
            uniqueueInstances.Add(instance);
        }

        if(uniqueueInstances.Count > 1)
        {
            Console.WriteLine("Thread safety failed!");
        }
        if(Singleton.InstanceCount > 1)
        {
            Console.WriteLine("Singleton class produced more than 1 instance!");
        }
    }
}
