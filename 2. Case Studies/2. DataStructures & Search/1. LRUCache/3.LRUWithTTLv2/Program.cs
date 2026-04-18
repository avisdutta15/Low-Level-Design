using _3.LRUWithTTLv2;

Console.WriteLine("=== Priority LRU — expired items evicted first (capacity=3) ===");
var pCache = new LRUCache(3);

pCache.Put(0, "zero", DateTimeOffset.UtcNow.AddSeconds(-5));    //add already expired item
Console.WriteLine($"Get 0: {pCache.Get(0)}");                   // "null" Shows eviction via GET.


//DEAD WEIGHT PROBLEM Solved
pCache.Put(1, "one", DateTimeOffset.UtcNow.AddSeconds(1));
pCache.Put(2, "two", DateTimeOffset.UtcNow.AddSeconds(1));
pCache.Put(3, "three", DateTimeOffset.UtcNow.AddHours(1));

//Cache(3,2,1)
Console.WriteLine($"Get 1: {pCache.Get(1)}"); // "one"
Console.WriteLine($"Get 2: {pCache.Get(2)}"); // "two"

// Cache is full (1, 2, 3)
Thread.Sleep(2000);
pCache.Put(4, "four", DateTimeOffset.UtcNow.AddHours(1));  //This evicts 1 and 2

// Cache(4, 1, 2)
Console.WriteLine($"Get 3: {pCache.Get(3)}"); // "three"