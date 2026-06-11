using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

//public class NonVolatileSingleton
//{
//    private static NonVolatileSingleton? _instance; // NOT volatile
//    private static readonly object _lock = new object();
//    private bool _initialized = false;

//    private NonVolatileSingleton()
//    {
//        // Simulate some initialization work
//        Thread.Sleep(100);
//        _initialized = true;
//        Console.WriteLine("Singleton initialized");
//    }

//    public static NonVolatileSingleton Instance
//    {
//        get
//        {
//            if (_instance == null) // First check (without lock)
//            {
//                lock (_lock)
//                {
//                    if (_instance == null) // Second check (with lock)
//                    {
//                        _instance = new NonVolatileSingleton();
//                    }
//                }
//            }
//            return _instance;
//        }
//    }

//    public void CheckInitialization()
//    {
//        if (!_initialized)
//        {
//            Console.WriteLine("ERROR: Singleton not properly initialized!");
//        }
//        else
//        {
//            Console.WriteLine("Singleton properly initialized");
//        }
//    }
//}

//public class Program
//{
//    public static void Main()
//    {
//        Console.WriteLine("=== Non-Volatile Singleton Race Condition ===");

//        // This can sometimes return a partially constructed object
//        Parallel.For(0, 10, i =>
//        {
//            var singleton = NonVolatileSingleton.Instance;
//            singleton.CheckInitialization();
//        });

//        Thread.Sleep(2000);
//    }
//}

