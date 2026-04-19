//namespace SimpleRateLimiters;

//public class RateLimitConfigurationTests
//{
//    public static void RunAllTests()
//    {
//        Console.WriteLine("=== RateLimitConfiguration Tests ===\n");
//        Test1_AddAndGetRule();
//        Test2_GetRuleFallsBackToDefault();
//        Test3_TryGetRuleReturnsFalseWhenNotFound();
//        Test4_ValidateRuleRejectsNullRule();
//        Test5_ValidateRuleRejectsNonPositiveRequestsAllowed();
//        Test6_ValidateRuleRejectsNonPositiveWindowSizeMs();
//        Test7_ValidateRuleRejectsNonPositiveBucketCapacity();
//        Test8_ValidateRuleRejectsNonPositiveRefillRate();
//        Test9_ValidateRuleRejectsNonPositiveRefillIntervalMs();
//        Test10_AddRuleUpdatesExistingRule();
//        Console.WriteLine("\n=== RateLimitConfiguration Tests Complete ===");
//    }
    
//    public static void Test1_AddAndGetRule()
//    {
//        Console.WriteLine("Test 1: Add and retrieve a rule");
//        var config = new RateLimitConfiguration();
//        var rule = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Premium,
//            Algorithm = RateLimitAlgorithm.TokenBucket,
//            RequestsAllowed = 100,
//            WindowSizeMs = 60000,
//            BucketCapacity = 100,
//            RefillRate = 100,
//            RefillIntervalMs = 60000
//        };
        
//        config.AddRule(rule);
//        var retrieved = config.GetRule(RateLimitDimension.UserId, RateLimitTier.Premium);
        
//        bool success = retrieved.Dimension == rule.Dimension &&
//                      retrieved.Tier == rule.Tier &&
//                      retrieved.Algorithm == rule.Algorithm &&
//                      retrieved.RequestsAllowed == rule.RequestsAllowed &&
//                      retrieved.WindowSizeMs == rule.WindowSizeMs &&
//                      retrieved.BucketCapacity == rule.BucketCapacity &&
//                      retrieved.RefillRate == rule.RefillRate &&
//                      retrieved.RefillIntervalMs == rule.RefillIntervalMs;
        
//        Console.WriteLine($"Result: {(success ? "PASS" : "FAIL")} - Rule retrieved matches added rule\n");
//    }
    
//    public static void Test2_GetRuleFallsBackToDefault()
//    {
//        Console.WriteLine("Test 2: GetRule falls back to default when rule not found");
//        var config = new RateLimitConfiguration();
//        var retrieved = config.GetRule(RateLimitDimension.ServiceId, RateLimitTier.Enterprise);
        
//        bool success = retrieved != null && retrieved.Tier == RateLimitTier.Default;
//        Console.WriteLine($"Result: {(success ? "PASS" : "FAIL")} - Default rule returned\n");
//    }
    
//    public static void Test3_TryGetRuleReturnsFalseWhenNotFound()
//    {
//        Console.WriteLine("Test 3: TryGetRule returns false when rule not found");
//        var config = new RateLimitConfiguration();
//        bool found = config.TryGetRule(RateLimitDimension.IpAddress, RateLimitTier.Free, out var rule);
        
//        bool success = !found && rule == null;
//        Console.WriteLine($"Result: {(success ? "PASS" : "FAIL")} - TryGetRule returned false\n");
//    }
    
//    public static void Test4_ValidateRuleRejectsNullRule()
//    {
//        Console.WriteLine("Test 4: Validate rule rejects null rule");
//        var config = new RateLimitConfiguration();
//        bool exceptionThrown = false;
        
//        try
//        {
//            config.AddRule(null!);
//        }
//        catch (ArgumentNullException)
//        {
//            exceptionThrown = true;
//        }
        
//        Console.WriteLine($"Result: {(exceptionThrown ? "PASS" : "FAIL")} - ArgumentNullException thrown\n");
//    }
    
//    public static void Test5_ValidateRuleRejectsNonPositiveRequestsAllowed()
//    {
//        Console.WriteLine("Test 5: Validate rule rejects non-positive RequestsAllowed");
//        var config = new RateLimitConfiguration();
//        var rule = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Free,
//            Algorithm = RateLimitAlgorithm.FixedWindow,
//            RequestsAllowed = 0,
//            WindowSizeMs = 60000,
//            BucketCapacity = 10,
//            RefillRate = 10,
//            RefillIntervalMs = 1000
//        };
        
//        bool exceptionThrown = false;
//        try
//        {
//            config.AddRule(rule);
//        }
//        catch (ArgumentException ex) when (ex.Message.Contains("RequestsAllowed"))
//        {
//            exceptionThrown = true;
//        }
        
//        Console.WriteLine($"Result: {(exceptionThrown ? "PASS" : "FAIL")} - ArgumentException thrown for RequestsAllowed\n");
//    }
    
//    public static void Test6_ValidateRuleRejectsNonPositiveWindowSizeMs()
//    {
//        Console.WriteLine("Test 6: Validate rule rejects non-positive WindowSizeMs");
//        var config = new RateLimitConfiguration();
//        var rule = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Free,
//            Algorithm = RateLimitAlgorithm.FixedWindow,
//            RequestsAllowed = 10,
//            WindowSizeMs = -1,
//            BucketCapacity = 10,
//            RefillRate = 10,
//            RefillIntervalMs = 1000
//        };
        
//        bool exceptionThrown = false;
//        try
//        {
//            config.AddRule(rule);
//        }
//        catch (ArgumentException ex) when (ex.Message.Contains("WindowSizeMs"))
//        {
//            exceptionThrown = true;
//        }
        
//        Console.WriteLine($"Result: {(exceptionThrown ? "PASS" : "FAIL")} - ArgumentException thrown for WindowSizeMs\n");
//    }
    
//    public static void Test7_ValidateRuleRejectsNonPositiveBucketCapacity()
//    {
//        Console.WriteLine("Test 7: Validate rule rejects non-positive BucketCapacity");
//        var config = new RateLimitConfiguration();
//        var rule = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Free,
//            Algorithm = RateLimitAlgorithm.TokenBucket,
//            RequestsAllowed = 10,
//            WindowSizeMs = 60000,
//            BucketCapacity = 0,
//            RefillRate = 10,
//            RefillIntervalMs = 1000
//        };
        
//        bool exceptionThrown = false;
//        try
//        {
//            config.AddRule(rule);
//        }
//        catch (ArgumentException ex) when (ex.Message.Contains("BucketCapacity"))
//        {
//            exceptionThrown = true;
//        }
        
//        Console.WriteLine($"Result: {(exceptionThrown ? "PASS" : "FAIL")} - ArgumentException thrown for BucketCapacity\n");
//    }
    
//    public static void Test8_ValidateRuleRejectsNonPositiveRefillRate()
//    {
//        Console.WriteLine("Test 8: Validate rule rejects non-positive RefillRate");
//        var config = new RateLimitConfiguration();
//        var rule = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Free,
//            Algorithm = RateLimitAlgorithm.TokenBucket,
//            RequestsAllowed = 10,
//            WindowSizeMs = 60000,
//            BucketCapacity = 10,
//            RefillRate = -5,
//            RefillIntervalMs = 1000
//        };
        
//        bool exceptionThrown = false;
//        try
//        {
//            config.AddRule(rule);
//        }
//        catch (ArgumentException ex) when (ex.Message.Contains("RefillRate"))
//        {
//            exceptionThrown = true;
//        }
        
//        Console.WriteLine($"Result: {(exceptionThrown ? "PASS" : "FAIL")} - ArgumentException thrown for RefillRate\n");
//    }
    
//    public static void Test9_ValidateRuleRejectsNonPositiveRefillIntervalMs()
//    {
//        Console.WriteLine("Test 9: Validate rule rejects non-positive RefillIntervalMs");
//        var config = new RateLimitConfiguration();
//        var rule = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Free,
//            Algorithm = RateLimitAlgorithm.TokenBucket,
//            RequestsAllowed = 10,
//            WindowSizeMs = 60000,
//            BucketCapacity = 10,
//            RefillRate = 10,
//            RefillIntervalMs = 0
//        };
        
//        bool exceptionThrown = false;
//        try
//        {
//            config.AddRule(rule);
//        }
//        catch (ArgumentException ex) when (ex.Message.Contains("RefillIntervalMs"))
//        {
//            exceptionThrown = true;
//        }
        
//        Console.WriteLine($"Result: {(exceptionThrown ? "PASS" : "FAIL")} - ArgumentException thrown for RefillIntervalMs\n");
//    }
    
//    public static void Test10_AddRuleUpdatesExistingRule()
//    {
//        Console.WriteLine("Test 10: AddRule updates existing rule for same dimension/tier");
//        var config = new RateLimitConfiguration();
//        var rule1 = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Free,
//            Algorithm = RateLimitAlgorithm.TokenBucket,
//            RequestsAllowed = 10,
//            WindowSizeMs = 60000,
//            BucketCapacity = 10,
//            RefillRate = 10,
//            RefillIntervalMs = 60000
//        };
        
//        var rule2 = new RateLimitRule
//        {
//            Dimension = RateLimitDimension.UserId,
//            Tier = RateLimitTier.Free,
//            Algorithm = RateLimitAlgorithm.FixedWindow,
//            RequestsAllowed = 20,
//            WindowSizeMs = 30000,
//            BucketCapacity = 20,
//            RefillRate = 20,
//            RefillIntervalMs = 30000
//        };
        
//        config.AddRule(rule1);
//        config.AddRule(rule2);
//        var retrieved = config.GetRule(RateLimitDimension.UserId, RateLimitTier.Free);
        
//        bool success = retrieved.Algorithm == RateLimitAlgorithm.FixedWindow &&
//                      retrieved.RequestsAllowed == 20 &&
//                      retrieved.WindowSizeMs == 30000;
        
//        Console.WriteLine($"Result: {(success ? "PASS" : "FAIL")} - Rule was updated\n");
//    }
//}
