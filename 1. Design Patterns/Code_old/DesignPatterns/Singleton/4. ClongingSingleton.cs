//using Xunit;

//namespace Singleton
//{
//    public sealed class CloningSingleton : ICloneable
//    {
//        private static CloningSingleton? _instance = null;
//        private static readonly object key = new object();
//        private static int _instanceCount = 0;

//        public static int InstanceCount => _instanceCount;
//        private CloningSingleton()
//        {
//            _instanceCount++;
//        }

//        public static CloningSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                lock (key)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new CloningSingleton();
//                    }
//                }
//            }
//            return _instance;
//        }

//        public object Clone()
//        {
//            // MemberwiseClone doesnot call constructor while cloning.
//            return this.MemberwiseClone();
//        }
//    }

//    public class CloningSingletonTests
//    {
//        [Fact]
//        public void GetInstance_OnCloning_CreatesDifferentInstances()
//        {
//            CloningSingleton instance = CloningSingleton.GetInstance();
//            CloningSingleton clonedInstance = (CloningSingleton)instance.Clone();

//            // Since its a member wise clone, the values types are copied as it is.
//            // Hence InstanceCount will remain 1 as the cloning doesnot trigger the constructor.
//            // But the instances will be different. instance.GetHashCode() and clonedInstance.GetHashCode() 
//            // will be different.
//            Assert.Same(instance, clonedInstance);
//        }
//    }

//    public sealed class FixedCloningSingleton : ICloneable
//    {
//        private static FixedCloningSingleton? _instance = null;
//        private static readonly object key = new object();
//        private static int _instanceCount = 0;

//        public static int InstanceCount => _instanceCount;
//        private FixedCloningSingleton()
//        {
//            _instanceCount++;
//        }

//        public static FixedCloningSingleton GetInstance()
//        {
//            if (_instance == null)
//            {
//                lock (key)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new FixedCloningSingleton();
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

//    public class FixedCloningSingletonTests
//    {
//        [Fact]
//        public void GetInstance_OnCloning_CreatesOneInstance()
//        {
//            FixedCloningSingleton instance = FixedCloningSingleton.GetInstance();
            
//            // Since its a member wise clone, the values types are copied as it is.
//            // Hence InstanceCount will remain 1 as the cloning doesnot trigger the constructor.
//            // But the instances will be different. instance.GetHashCode() and clonedInstance.GetHashCode() 
//            // will be different.
//            Assert.Throws<NotSupportedException>(()=>
//            {
//                FixedCloningSingleton clonedInstance = (FixedCloningSingleton)instance.Clone();
//            });
//        }
//    }
//}
