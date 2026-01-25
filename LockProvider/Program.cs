using System.Collections.Concurrent;

public interface ILockProvider
{
    /// <summary>
    /// Returns true if the key was successfully locked
    /// </summary>
    /// <param name="key">The key that you want to lock</param>
    /// <param name="userId">The owner of the locked key</param>
    /// <param name="ttlMs">Time to live for the lock</param>
    /// <returns></returns>
    public bool TryLock(string key, string userId, long ttlMs);
    /// <summary>
    /// Unlock a given key
    /// </summary>
    /// <param name="key"></param>
    public void Unlock(string key);
    /// <summary>
    /// Returns true if the lock is expired for the given key
    /// </summary>
    /// <param name="key"></param>
    public bool IsLockExpired(string key);
    /// <summary>
    /// Returns true if the key is locked by the given user
    /// </summary>
    /// <param name="key"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public bool IsLockedBy(string key, string userId);
}

public class InMemoryLockProvider : ILockProvider
{
    private record Lock(DateTime ExpiresAt, string OwnerId);
    private readonly ConcurrentDictionary<string, Lock> _lockCache;

    public InMemoryLockProvider()
    {
        _lockCache = new();

        // Start a background cleanup task immediately (Fire and Forget)
        // This runs automatically to clean up zombies every 1 minute.
        Task.Run(async () => 
        {
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
            while (await timer.WaitForNextTickAsync())
            {
                CleanUpZombieLocks();
            }
        });
    }

    private void CleanUpZombieLocks()
    {
        DateTime now = DateTime.UtcNow;
        foreach(var kvp in _lockCache)
        {
            if(kvp.Value.ExpiresAt <= now)
            {
                //The following has ABA problem
                //_lockCache.TryRemove(kvp.Key, out _);

                // Use explicit Remove with comparison to avoid removing if value changed
                // Safe Removal: effectively says "Delete this key ONLY if 
                // the value hasn't changed since I last looked at it."
                ((ICollection<KeyValuePair<string, Lock>>)_lockCache).Remove(kvp);
            }
        }
    }

    public bool TryLock(string key, string userId, long ttlMs)
    {
        DateTime now = DateTime.UtcNow;
        var newLock = new Lock(ExpiresAt: now.AddMilliseconds(ttlMs), OwnerId: userId);

        // Atomic operation:
        // 1. If key missing -> Add newLock.
        // 2. If key exists -> Run lambda.
        var effectiveLock = _lockCache.AddOrUpdate(key, newLock, (key, currentLock) =>
        {
            // If the current lock is still valid, keep it (do not overwrite).
            if(currentLock.ExpiresAt > now)
            {
                return currentLock;
            }
            // If current lock is expired, overwrite it with our new lock.
            return newLock;
        });

        // If the value currently in the dictionary is the specific instance 
        // we just created, that means we were successful in setting the lock.
        // using ReferenceEquals ensures we don't accidentally match a different 
        // lock record that happens to have the exact same time/user.
        // Why it works:
        //1. Records are reference types: Even though records have value - based equality,
        //   they're still reference types allocated on the heap with distinct identities.
        //2. Reference identity check: ReferenceEquals(newLock, effectiveLock) returns
        //   true only if effectiveLock is the exact same object instance as newLock -
        //   meaning your lock was successfully stored in the dictionary.
        //3. Atomic guarantee: AddOrUpdate guarantees that only one thread's update function
        //   will successfully store a value for a given key at a time. If your newLock
        //   instance is what got stored, you won the race.
        return ReferenceEquals(newLock, effectiveLock);
    }

    public void Unlock(string key)
    {
        _lockCache.TryRemove(key, out _);
    }

    public bool IsLockedBy(string key, string userId)
    {
        if (_lockCache.TryGetValue(key, out var _lock))
        {
            // Check if it exists, matches user, AND is not expired
            return (_lock != null && _lock.OwnerId == userId && _lock.ExpiresAt > DateTime.UtcNow);
        }
        return false;
    }

    public bool IsLockExpired(string key)
    {
        if (_lockCache.TryGetValue(key, out var _lock))
        {
            return (_lock != null && _lock.ExpiresAt <= DateTime.UtcNow);
        }
        return false;
    }
}

public class RedisLockProvider : ILockProvider
{
    public bool IsLockedBy(string key, string userId)
    {
        throw new NotImplementedException();
    }

    public bool IsLockExpired(string key)
    {
        throw new NotImplementedException();
    }

    public bool TryLock(string key, string userId, long ttlMs)
    {
        throw new NotImplementedException();
    }

    public void Unlock(string key)
    {
        throw new NotImplementedException();
    }
}

class Program
{
    static int passedTests = 0;
    static int failedTests = 0;

    static void Main(string[] args)
    {
        Console.WriteLine("=== LockProvider Test Suite ===\n");

        // Basic functionality tests
        Test_BasicLockAcquisition();
        Test_LockByDifferentUser();
        Test_LockExpiration();
        Test_UnlockOperation();
        Test_IsLockedBy();
        Test_IsLockExpired();
        
        // Edge cases
        Test_ZeroTTL();
        Test_LongTTL();
        Test_SameKeyMultipleUsers();
        Test_RelockAfterExpiration();
        Test_UnlockNonExistentKey();
        
        // Concurrency tests
        Test_ConcurrentLockAttempts();
        Test_ConcurrentLockDifferentKeys();
        Test_RapidLockUnlockCycles();
        Test_ConcurrentExpirationCheck();

        // Summary
        Console.WriteLine("\n=== Test Summary ===");
        Console.WriteLine($"Passed: {passedTests}");
        Console.WriteLine($"Failed: {failedTests}");
        Console.WriteLine($"Total: {passedTests + failedTests}");
        
        if (failedTests == 0)
        {
            Console.WriteLine("\n✓ All tests passed!");
        }
        else
        {
            Console.WriteLine($"\n✗ {failedTests} test(s) failed!");
        }
    }

    static void Assert(bool condition, string testName, string message = "")
    {
        if (condition)
        {
            passedTests++;
            Console.WriteLine($"✓ {testName}");
        }
        else
        {
            failedTests++;
            Console.WriteLine($"✗ {testName} - {message}");
        }
    }

    static void Test_BasicLockAcquisition()
    {
        var provider = new InMemoryLockProvider();
        bool locked = provider.TryLock("resource1", "user1", 5000);
        Assert(locked, "Test_BasicLockAcquisition", "Should acquire lock on first attempt");
    }

    static void Test_LockByDifferentUser()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 5000);
        bool locked = provider.TryLock("resource1", "user2", 5000);
        Assert(!locked, "Test_LockByDifferentUser", "Should not acquire lock held by another user");
    }

    static void Test_LockExpiration()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 100); // 100ms TTL
        Thread.Sleep(150); // Wait for expiration
        bool locked = provider.TryLock("resource1", "user2", 5000);
        Assert(locked, "Test_LockExpiration", "Should acquire lock after expiration");
    }

    static void Test_UnlockOperation()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 5000);
        provider.Unlock("resource1");
        bool locked = provider.TryLock("resource1", "user2", 5000);
        Assert(locked, "Test_UnlockOperation", "Should acquire lock after unlock");
    }

    static void Test_IsLockedBy()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 5000);
        bool isLockedByUser1 = provider.IsLockedBy("resource1", "user1");
        bool isLockedByUser2 = provider.IsLockedBy("resource1", "user2");
        Assert(isLockedByUser1 && !isLockedByUser2, "Test_IsLockedBy", 
            "Should correctly identify lock owner");
    }

    static void Test_IsLockExpired()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 100);
        bool notExpiredYet = !provider.IsLockExpired("resource1");
        Thread.Sleep(150);
        bool expiredNow = provider.IsLockExpired("resource1");
        Assert(notExpiredYet && expiredNow, "Test_IsLockExpired", 
            "Should correctly detect lock expiration");
    }

    static void Test_ZeroTTL()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 0);
        Thread.Sleep(10);
        bool locked = provider.TryLock("resource1", "user2", 5000);
        Assert(locked, "Test_ZeroTTL", "Lock with zero TTL should expire immediately");
    }

    static void Test_LongTTL()
    {
        var provider = new InMemoryLockProvider();
        bool locked1 = provider.TryLock("resource1", "user1", 3600000); // 1 hour
        bool locked2 = provider.TryLock("resource1", "user2", 5000);
        Assert(locked1 && !locked2, "Test_LongTTL", "Long TTL lock should remain valid");
    }

    static void Test_SameKeyMultipleUsers()
    {
        var provider = new InMemoryLockProvider();
        bool user1 = provider.TryLock("resource1", "user1", 5000);
        bool user2 = provider.TryLock("resource1", "user2", 5000);
        bool user3 = provider.TryLock("resource1", "user3", 5000);
        Assert(user1 && !user2 && !user3, "Test_SameKeyMultipleUsers", 
            "Only first user should acquire lock");
    }

    static void Test_RelockAfterExpiration()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 100);
        Thread.Sleep(150);
        bool locked = provider.TryLock("resource1", "user1", 5000);
        Assert(locked, "Test_RelockAfterExpiration", 
            "Same user should be able to relock after expiration");
    }

    static void Test_UnlockNonExistentKey()
    {
        var provider = new InMemoryLockProvider();
        try
        {
            provider.Unlock("nonexistent");
            Assert(true, "Test_UnlockNonExistentKey", "Should handle unlock of non-existent key");
        }
        catch
        {
            Assert(false, "Test_UnlockNonExistentKey", "Should not throw on non-existent key");
        }
    }

    static void Test_ConcurrentLockAttempts()
    {
        var provider = new InMemoryLockProvider();
        int successCount = 0;
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            int userId = i;
            tasks.Add(Task.Run(() =>
            {
                if (provider.TryLock("resource1", $"user{userId}", 5000))
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        Assert(successCount == 1, "Test_ConcurrentLockAttempts", 
            $"Only one thread should acquire lock, got {successCount}");
    }

    static void Test_ConcurrentLockDifferentKeys()
    {
        var provider = new InMemoryLockProvider();
        int successCount = 0;
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            int userId = i;
            tasks.Add(Task.Run(() =>
            {
                if (provider.TryLock($"resource{userId}", $"user{userId}", 5000))
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        Assert(successCount == 10, "Test_ConcurrentLockDifferentKeys", 
            $"All threads should acquire different locks, got {successCount}");
    }

    static void Test_RapidLockUnlockCycles()
    {
        var provider = new InMemoryLockProvider();
        bool allSucceeded = true;

        for (int i = 0; i < 100; i++)
        {
            bool locked = provider.TryLock("resource1", "user1", 5000);
            if (!locked)
            {
                allSucceeded = false;
                break;
            }
            provider.Unlock("resource1");
        }

        Assert(allSucceeded, "Test_RapidLockUnlockCycles", 
            "Should handle rapid lock/unlock cycles");
    }

    static void Test_ConcurrentExpirationCheck()
    {
        var provider = new InMemoryLockProvider();
        provider.TryLock("resource1", "user1", 100);
        
        var tasks = new List<Task<bool>>();
        Thread.Sleep(150); // Ensure lock is expired

        for (int i = 0; i < 10; i++)
        {
            int userId = i;
            tasks.Add(Task.Run(() => provider.TryLock("resource1", $"user{userId}", 5000)));
        }

        Task.WaitAll(tasks.ToArray());
        int successCount = tasks.Count(t => t.Result);

        Assert(successCount == 1, "Test_ConcurrentExpirationCheck", 
            $"Only one thread should acquire expired lock, got {successCount}");
    }
}
