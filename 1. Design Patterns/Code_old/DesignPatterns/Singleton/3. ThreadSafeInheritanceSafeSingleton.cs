//namespace Singleton
//{
//    public sealed class ThreadSafeInheritanceSafeSingleton
//    {
//        private static ThreadSafeInheritanceSafeSingleton? _instance;
//        private static int _instanceCount = 0;
//        private static readonly object key = new object();
//        public static int InstanceCount => _instanceCount;

//        private ThreadSafeInheritanceSafeSingleton()
//        {
//            _instanceCount++;
//        }

//        public static ThreadSafeInheritanceSafeSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                lock (key)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new ThreadSafeInheritanceSafeSingleton();
//                    }
//                }
//            }
//            return _instance;
//        }

//        //Other methods here

//        /*
//        *   Since the class is sealed the following gives error: Cannot derive from sealed type
//        *   public class DerivedClass : ThreadSafeInheritanceSafeSingleton
//        *   {
//        *       public DerivedClass() : base()
//        *       {
//        *       }
//        *   }
//        */
//    }
//}
