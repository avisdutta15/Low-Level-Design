//using System.Reflection;
//using Xunit;

//namespace Singleton
//{
//    public sealed class ReflectionSingleton : ICloneable
//    {
//        private static ReflectionSingleton? _instance = null;
//        private static readonly object key = new object();
//        private static int _instanceCount = 0;

//        public static int InstanceCount => _instanceCount;
//        private ReflectionSingleton()
//        {
//            _instanceCount++;
//        }

//        public static ReflectionSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                lock (key)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new ReflectionSingleton();
//                    }
//                }
//            }
//            return _instance;
//        }

//        public object Clone()
//        {
//            // Prevent Cloning
//            throw new NotSupportedException("Singleton instance cannot be cloned.");
//        }
//    }

//    public class ReflectionSingletonTests
//    {
//        [Fact]
//        public void GetInstance_OnReflection_CreatesNewInstance()
//        {
//            // Get the real singleton instance
//            var singleton1 = ReflectionSingleton.GetInstance();

//            // Get the private constructor
//            var constructor = typeof(ReflectionSingleton).GetConstructor(
//                BindingFlags.NonPublic | BindingFlags.Instance,
//                null, Type.EmptyTypes, null);

//            if (constructor != null)
//            {
//                // Create multiple instances using reflection
//                var reflectedInstance1 = (ReflectionSingleton)constructor.Invoke(null);
//                var reflectedInstance2 = (ReflectionSingleton)constructor.Invoke(null);

//                Assert.True(ReflectionSingleton.InstanceCount == 1);

//                Assert.Same(singleton1,  reflectedInstance1);
//                Assert.Same(singleton1,  reflectedInstance2);
//            }
//        }

//    }

//    public sealed class FixedReflectionSingleton : ICloneable
//    {
//        private static FixedReflectionSingleton? _instance = null;
//        private static readonly object key = new object();
//        private static int _instanceCount = 0;
//        private static bool _isFirstInstance = false;

//        public static int InstanceCount => _instanceCount;
//        private FixedReflectionSingleton()
//        {
//            if (_isFirstInstance == true)
//            {
//                throw new InvalidOperationException("Cannot create more than one instance of singleton class.");
//            }
//            _isFirstInstance = true;
//            _instanceCount++;
//        }

//        public static FixedReflectionSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                lock (key)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new FixedReflectionSingleton();
//                    }
//                }
//            }
//            return _instance;
//        }

//        public object Clone()
//        {
//            // Prevent Cloning
//            throw new NotSupportedException("Singleton instance cannot be cloned.");
//        }
//    }

//    public class FixedReflectionSingletonTests
//    {
//        [Fact]
//        public void GetInstance_OnReflection_CreatesNewInstance()
//        {
//            // Get the real singleton instance
//            var singleton1 = FixedReflectionSingleton.GetInstance();

//            // Get the private constructor
//            var constructor = typeof(FixedReflectionSingleton).GetConstructor(
//                BindingFlags.NonPublic | BindingFlags.Instance,
//                null, Type.EmptyTypes, null);

//            if (constructor != null)
//            {
//                // Create multiple instances using reflection
//                var reflectedInstance1 = (FixedReflectionSingleton)constructor.Invoke(null);
//                var reflectedInstance2 = (FixedReflectionSingleton)constructor.Invoke(null);

//                Assert.True(ReflectionSingleton.InstanceCount == 1);

//                Assert.Same(singleton1, reflectedInstance1);
//                Assert.Same(singleton1, reflectedInstance2);
//            }
//        }

//    }
//}
