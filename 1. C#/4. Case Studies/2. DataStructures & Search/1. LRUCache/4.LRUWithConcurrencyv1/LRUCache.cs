namespace _4.LRUWithConcurrencyv1;

public class LRUCache : IDisposable
{
    private readonly Dictionary<int, Node> _map;
    private readonly DoublyLinkedList _list;
    private int _size;
    private readonly int _capacity;

    private readonly Thread _purgeThread;   //background thread
    private CancellationTokenSource _cts;
    private static readonly object _cacheLock = new object();
    private TimeSpan _purgeInterval;
    private int _disposed = 0; // 0 = alive, 1 = disposed (for Interlocked)

    public LRUCache(int capacity, TimeSpan? purgeInterval = null)
    {
        _map = new Dictionary<int, Node>();
        _list = new DoublyLinkedList();
        _purgeThread = new Thread(() => PurgeExpiredKeys())
        {
            IsBackground = true,
            Name = "LRUCache-PurgeThread"
        };
        _cts = new CancellationTokenSource();
        _size = 0;
        _capacity = capacity;
        _purgeInterval = purgeInterval ?? TimeSpan.FromSeconds(1);

        //Start the thread
        _purgeThread.Start();
    }

    public string Get(int key)
    {
        lock (_cacheLock)
        {
            if (_map.TryGetValue(key, out Node? node) == false)
                return "null";

            // check if the node has expired. Lazy Expiration
            if (node.HasExpired())
            {
                _map.Remove(key);
                _list.RemoveNode(node);
                _size--;
                return "null";
            }

            // Promote to MRU
            _list.RemoveNode(node);
            _list.AddFirst(node);
            return node.Value;
        }
    }

    /// <summary>
    /// Convenience overload — items with no explicit TTL get DateTimeOffset.MaxValue
    /// (effectively never expire).
    /// </summary>
    public void Put(int key, string value)
    {
        Put(key, value, DateTimeOffset.MaxValue);
    }

    public void Put(int key, string value, DateTimeOffset expiresAt)
    {
        // check if the key exists in the hashtable
        // if yes then get the node from the hash table,
        //      detach it from the list
        //      insert it to the head of the list
        //      update the node's value
        // if no then create a new node
        //      add the key and node to the hashtable
        //      if the cache is at capacity
        //          remove the tail node from the list
        //      insert the new node to the head of the list

        lock (_cacheLock) 
        {
            // Update existing key
            if (_map.TryGetValue(key, out Node? node) != false)
            {
                //Promote to MRU
                _list.RemoveNode(node);

                // Update the nodes values 
                node.Value = value;
                node.ExpiresAt = expiresAt;

                //Promote to MRU
                _list.AddFirst(node);
                return;
            }
            else
            {
                //Evict if cache is full
                if (_size >= _capacity)
                {
                    LRUEvict();

                }
            
                // Insert the new node
                Node newNode = new Node(key, value, expiresAt);
                _map.Add(key, newNode);
                _list.AddFirst(newNode);
                _size++;
            }
        }        
    }

    /// <summary>
    /// Basic Housekeeping.
    /// while(true)
    ///     sleep
    ///     wakeup and check token.ThrowIfCancellationRequested();
    ///     remove all expired entries from the cache
    /// </summary>
    public void PurgeExpiredKeysBasic()
    {
        var token = _cts.Token;

        //infinite loop
        while (true)
        {
            try
            {
                // Sleep : Issue - When cancellation triggered, the Thread is hot woke up
                // immediately. It completes its sleep interval.
                Thread.Sleep(_purgeInterval);

                //Check if cancellation triggered
                token.ThrowIfCancellationRequested();

                //else do the job
                lock (_cacheLock)
                {
                    //Collect all the keys from the map that have expired.
                    List<Node> expiredKeys = new List<Node>();
                    foreach (var entry in _map)
                    {
                        if (entry.Value.HasExpired())
                            expiredKeys.Add(entry.Value);
                    }

                    //Remove them from map and list
                    foreach (Node node in expiredKeys)
                    {
                        _map.Remove(node.Key);
                        _list.RemoveNode(node);
                        _size--;
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine(e.Message);
                break;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }

    public void PurgeExpiredKeys()
    {
        var token = _cts.Token;

        // WaitOne pauses the thread. But wakes it up immediately when signalled for cancellation. 
        // It returns true if cancelled (breaking the loop). 
        // It returns false if the interval passed (continuing the loop).
        while (!token.WaitHandle.WaitOne(_purgeInterval))
        {
            try
            {
                lock (_cacheLock)
                {
                    //Collect all the keys from the map that have expired.
                    List<Node> expiredKeys = new List<Node>();
                    foreach (var entry in _map)
                    {
                        if (entry.Value.HasExpired())
                            expiredKeys.Add(entry.Value);
                    }

                    //Remove them from map and list
                    foreach (Node node in expiredKeys)
                    {
                        _map.Remove(node.Key);
                        _list.RemoveNode(node);
                        _size--;
                    }
                }
            }
            catch (Exception e)
            {
                // Catch ALL exceptions. Log them, but let the while loop continue.
                // This guarantees the housekeeper thread never dies unexpectedly.
                Console.WriteLine(e.Message);
            }            
        }
        Console.WriteLine("Purge routine cancelled. Exiting gracefully.");
    }

    public void LRUEvict()
    {
        Node lastNode = _list.Last;
        if (lastNode != _list.head)  // safety: don't remove the dummy head
        {
            _list.RemoveNode(lastNode);
            _map.Remove(lastNode.Key);
            _size--;
        }
    }

    public void Dispose()
    {
        /* This is not atomic
        if (_disposed == 1)
            return;
        _disposed = 1;
        */

        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;


        // Wake up the background thread immediately and tell it to exit the while loop
        _cts.Cancel();

        // Wait for it to finish its current iteration safely
        if (_purgeThread.IsAlive)
            _purgeThread.Join();
        
        _cts.Dispose();
    }
}

