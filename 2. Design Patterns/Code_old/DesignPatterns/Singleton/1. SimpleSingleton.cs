//using Xunit;

//namespace Singleton
//{
//    public class SimpleSingleton
//    {
//        private static SimpleSingleton? _instance;
//        private static int _instanceCount = 0;
//        public static int InstanceCount => _instanceCount;
        
//        private SimpleSingleton() 
//        {
//            _instanceCount++;
//        }

//        public static SimpleSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                _instance = new SimpleSingleton();
//            }
//            return _instance;
//        }

//        //Other methods here
//    }

//    public class SimpleSingletonTests
//    {
//        [Fact]
//        public void GetInstance_AlwaysReturnsSameInstance()
//        {
//            // Arrange & Act
//            // 1. Get the first instance
//            SimpleSingleton instance1 = SimpleSingleton.GetInstance();

//            // 2. Get a second instance
//            SimpleSingleton instance2 = SimpleSingleton.GetInstance();

//            // Assert

//            // **Assertion 1: Check if the instances are not null**
//            Assert.NotNull(instance1);
//            Assert.NotNull(instance2);

//            // **Assertion 2: Check if both references point to the exact same object**
//            Assert.Equal(instance1, instance2);
//        }

//        [Fact]
//        public async Task GetInstance_ConcurrentAccess_CreatesMoreThanOneInstance()
//        {
//            //1. Arrange
//            var taskList = new List<Task<SimpleSingleton>>();
//            var uniqueInstances = new HashSet<SimpleSingleton>();

//            //2. Act
//            // Create an array of tasks, each task calls GetInstance()
//            Parallel.For(0, 100, (_) =>
//            {
//                taskList.Add(Task.Run(() => 
//                {
//                    return SimpleSingleton.GetInstance();
//                }));
//            });

//            // Wait for all tasks to complete
//            await Task.WhenAll(taskList);

//            //Collect the result
//            var instances = taskList.Select(task=> task.Result).ToList();

//            // Add all returned instances to a HashSet to count unique references
//            foreach (var instance in instances)
//            {
//                uniqueInstances.Add(instance);
//            }

//            //3. Assert
//            Assert.True(condition: uniqueInstances.Count == 1, userMessage: "Thread Safety check failed");
//            Assert.True(condition: SimpleSingleton.InstanceCount == 1, userMessage: "More than 1 instance created!");
//        }

//        [Fact]
//        public void GetInstance_Serialization_CreatesMoreThanOnceInstance()
//        {

//        }
//    }
//}
