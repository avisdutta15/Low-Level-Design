using _1.LRUWithoutTTL;

Console.WriteLine("=== Basic LRU (capacity=2) ===");
var cache = new LRUCache(2);

Console.WriteLine($"Put 1"); cache.Put(1, "one");
Console.WriteLine($"Put 2"); cache.Put(2, "two");
Console.WriteLine($"Get 1: {cache.Get(1)}");   // "one" — promotes 1 to MRU

Console.WriteLine($"Put 3"); cache.Put(3, "three"); // evicts 2 (LRU)
Console.WriteLine($"Get 2: {cache.Get(2)}");    // null — evicted
Console.WriteLine($"Get 3: {cache.Get(3)}");    // "three"

Console.WriteLine();
