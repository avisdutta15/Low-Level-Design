namespace Singleton
{
    // Sealed prevents attacks from Inheritance
    public sealed class Singleton : ICloneable
    {
        private static volatile Singleton? _instance = null;
        private static readonly object key = new object();
        private static int _instanceCount = 0;
        private static bool _isFirstInstance = false;

        public static int InstanceCount => _instanceCount;
        private Singleton()
        {
            // Static field prevents Attacks from Reflection
            if (_isFirstInstance == true)
            {
                throw new InvalidOperationException("Cannot create more than one instance of singleton class.");
            }
            _isFirstInstance = true;
            _instanceCount++;
        }
        public static Singleton GetInstance()
        {
            // Double checking locks
            if (_instance == null)
            {
                lock (key)
                {
                    if (_instance == null)
                    {
                        _instance = new Singleton();
                    }
                }
            }
            return _instance;
        }
        public object Clone()
        {
            // Prevent Cloning
            throw new NotSupportedException("Singleton instance cannot be cloned.");
        }
    }

    public class SingletonDemo
    {
        public static void Main()
        {
            Singleton instance1 = Singleton.GetInstance();
            Singleton instance2 = Singleton.GetInstance();

            Console.WriteLine($"{instance1 == instance2}");
        }
    }
}

/*
 * SUMMARY:
 * 1. Singleton means single instance of the object.
 * 2. There are ways in which we can create multiple objects of a class. Each categorized into 2 parts - calling constructor, without calling constructor
 * 3. Calling Constructor Attacks & Measures
 *      a. Private Constructor - Prevents instantiation from outside the class. 
 *                               We will have a static member called instance that can be accessed by static GetInstance() method.
 *      b. Sealed Class - Prevents inheritance attacks (nested subclasses creating instances)
 *      c. Reflection Protection - Static flag throws exception if constructor is called via reflection after first instance.
 * 4. Without Calling Constructor Attacks & Measures
 *      a. Cloning - Implement ICloneable and throw NotSupportedException
 *      b. Serialization - (Not shown) Override GetObjectData or use [NonSerialized]
 * 5. Making Singleton ThreadSafe:
 *      A. Use Locks to guard the instance creation
 *      B. Double Lock Checking - Check null before and after lock for performance                         
 *                              
 *                              
 * 6. Key Implementation Points:
 *      - Private static volatile field for instance
 *      - Private static readonly object for lock
 *      - Private constructor with reflection protection
 *      - Public static method with double-check locking
 *      - Sealed class to prevent inheritance
 *      - Clone method that throws exception    
 *      
 * Note: Need of volatile
 * In C#, without the volatile keyword, the double-check locking pattern might not be thread safe due to instruction reordering.
 * The problem is that the CPU or compiler might reorder the instructions in such a way that the _instance reference is set before the constructor has finished executing.
 * This could lead to other threads seeing a non-null _instance but the object is not fully constructed.
 * 
 * However, note that in C# (from version 5.0 onwards) the behavior of the volatile keyword is well-defined and ensures that writes are not reordered.
 * 
 * But without volatile, the JIT compiler might optimize the code and cause a race condition.
 * 
 * Let me explain:
 * The double-check locking pattern without volatile can be broken because:
 * Thread A enters the lock, sees _instance is null, and starts constructing the object.
 * The memory for the object is allocated, and the reference is assigned to _instance (but the constructor hasn't finished running yet) due to reordering.
 * Thread B comes and checks _instance (without entering the lock) and sees it is not null, so it returns the instance that is not fully constructed.
 * 
 * Using volatile prevents this reordering because it introduces a memory barrier, ensuring that the write to _instance happens only after the constructor has completed.
 *  
 *  
 *  LLD UseCase:
 *  Make the final facade service/manger class as Singleton.
 *  This class you will use to interact with the system.
 *  i.e. LibraryMangementService.
 *       MovieBookingSerice
 *       RateLimiterService
 */
