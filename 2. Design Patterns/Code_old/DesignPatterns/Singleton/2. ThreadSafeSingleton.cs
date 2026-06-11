//using Xunit;

//namespace Singleton
//{
//    public class ThreadSafeSingleton
//    {
//        private static ThreadSafeSingleton? _instance;
//        private static int _instanceCount = 0;
//        private static readonly object key = new object();
//        public static int InstanceCount => _instanceCount;

//        private ThreadSafeSingleton()
//        {
//            _instanceCount++;
//        }

//        public static ThreadSafeSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                lock (key)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new ThreadSafeSingleton();
//                    }
//                }
//            }
//            return _instance;
//        }

//        //Other methods here
//    }

//    public class ThreadSafeInheritanceUnsafeSingleton
//    {
//        private static ThreadSafeInheritanceUnsafeSingleton? _instance;
//        private static int _instanceCount = 0;
//        private static readonly object key = new object();
//        public static int InstanceCount => _instanceCount;

//        private ThreadSafeInheritanceUnsafeSingleton()
//        {
//            _instanceCount++;
//        }

//        public static ThreadSafeInheritanceUnsafeSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                lock (key)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new ThreadSafeInheritanceUnsafeSingleton();
//                    }
//                }
//            }
//            return _instance;
//        }

//        //Other methods here
//        public class DerivedClass : ThreadSafeInheritanceUnsafeSingleton
//        {
//            public DerivedClass(): base()
//            {

//            }
//        }
//    }

//    public class ThreadSafeSingletonTests
//    {
//        [Fact]
//        public void GetInstance_AlwaysReturnsSameInstance()
//        {
//            // Arrange & Act
//            // 1. Get the first instance
//            ThreadSafeSingleton instance1 = ThreadSafeSingleton.GetInstance();

//            // 2. Get a second instance
//            ThreadSafeSingleton instance2 = ThreadSafeSingleton.GetInstance();

//            // Assert

//            // **Assertion 1: Check if the instances are not null**
//            Assert.NotNull(instance1);
//            Assert.NotNull(instance2);

//            // **Assertion 2: Check if both references point to the exact same object**
//            Assert.Equal(instance1, instance2);
//        }

//        [Fact]
//        public async Task GetInstance_ConcurrentAccess_CreatesOneInstance()
//        {
//            //1. Arrange
//            var taskList = new List<Task<ThreadSafeSingleton>>();
//            var uniqueInstances = new HashSet<ThreadSafeSingleton>();

//            //2. Act
//            // Create an array of tasks, each task calls GetInstance()
//            Parallel.For(0, 100, (_) =>
//            {
//                taskList.Add(Task.Run(() =>
//                {
//                    return ThreadSafeSingleton.GetInstance();
//                }));
//            });

//            // Wait for all tasks to complete
//            await Task.WhenAll(taskList);

//            //Collect the result
//            var instances = taskList.Select(task => task.Result).ToList();

//            // Add all returned instances to a HashSet to count unique references
//            foreach (var instance in instances)
//            {
//                uniqueInstances.Add(instance);
//            }

//            //3. Assert
//            Assert.True(condition: uniqueInstances.Count == 1, userMessage: "Thread Safety check failed");
//            Assert.True(condition: ThreadSafeSingleton.InstanceCount == 1, userMessage: "More than 1 instance created!");
//        }

//        [Fact]
//        public void GetInstance_OnDeriving_CreatesMoreThanOneInstance()
//        {

//        }
//    }

//    public class ThreadSafeInheritanceUnsafeSingletonTests
//    {
//        [Fact]
//        public void GetInstance_OnInheriting_CreatesMoreThanOneInstance()
//        {
//            //1. Arrange
//            ThreadSafeInheritanceUnsafeSingleton.DerivedClass? derivedObj = null;

//            //2. Act
//            ThreadSafeInheritanceUnsafeSingleton.GetInstance();
//            derivedObj = new();

//            //3. Assert
//            Assert.True(ThreadSafeInheritanceUnsafeSingleton.InstanceCount == 1, $"Instance Count : {ThreadSafeInheritanceUnsafeSingleton.InstanceCount}");
//        }
//    }
//}
