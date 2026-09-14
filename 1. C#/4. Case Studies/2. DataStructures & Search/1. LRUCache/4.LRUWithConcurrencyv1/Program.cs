using _4.LRUWithConcurrencyv1;

Console.WriteLine("=== Background Purge LRU (capacity=3, purge every 500ms) ===");
using var bgCache = new LRUCache(3, purgeInterval: TimeSpan.FromMilliseconds(500));

// Item 1 expires in 1 second, items 2 & 3 are long-lived
bgCache.Put(1, "one", DateTimeOffset.UtcNow.AddSeconds(1));
bgCache.Put(2, "two", DateTimeOffset.UtcNow.AddHours(1));
bgCache.Put(3, "three", DateTimeOffset.UtcNow.AddHours(1));

Console.WriteLine($"Get 1 (before expiry): {bgCache.Get(1)}"); // "one"

// Wait for item 1 to expire and the background thread to purge it
Console.WriteLine("Waiting 2s for background purge...");
Thread.Sleep(2000);

Console.WriteLine($"Get 1 (after purge):   {bgCache.Get(1)}"); // "null" — purged by background thread

// Cache now has 2 items (2, 3). Adding 4 won't evict anything.
bgCache.Put(4, "four", DateTimeOffset.UtcNow.AddHours(1));
Console.WriteLine($"Get 2 (still alive):   {bgCache.Get(2)}"); // "two"
Console.WriteLine($"Get 3 (still alive):   {bgCache.Get(3)}"); // "three"
Console.WriteLine($"Get 4 (just added):    {bgCache.Get(4)}"); // "four"