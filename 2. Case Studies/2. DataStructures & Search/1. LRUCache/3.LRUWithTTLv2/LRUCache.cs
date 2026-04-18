namespace _3.LRUWithTTLv2;

/// <summary>
/// An LRU Cache that uses a PriorityQueue to evict expired items first,
/// falling back to standard LRU (tail) eviction only when no expired items exist.
/// Set is O(log N) due to priority queue insertion; Get remains O(1).
/// </summary>
public class LRUCache
{
    private readonly Dictionary<int, Node> _map;
    private readonly DoublyLinkedList _list;
    private readonly PriorityQueue<int, DateTimeOffset> _expiryQueue;   //<key, expirationTime>
    private readonly int _capacity;
    private int _size;

    public LRUCache(int capacity)
    {
        _map = new Dictionary<int, Node>();
        _list = new DoublyLinkedList();
        _expiryQueue = new PriorityQueue<int, DateTimeOffset>();
        _capacity = capacity;
    }

    public string Get(int key)
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

            // Re-enqueue with updated expiry (stale entries are handled lazily)
            _expiryQueue.Enqueue(key, expiresAt);
            return;
        }
        else
        {
            //Evict if cache is full
            if (_size >= _capacity)
            {
                PurgeExpiredKeys();

                // Only LRU-evict if purging didn't free enough space
                if (_size >= _capacity)
                {
                    LRUEvict();
                }
            }

            // Insert the new node
            Node newNode = new Node(key, value, expiresAt);
            _map.Add(key, newNode);
            _list.AddFirst(newNode);
            _expiryQueue.Enqueue(key, expiresAt);
            _size++;
        }
    }

    public void PurgeExpiredKeys()
    {
        while (_expiryQueue.Count > 0)
        {
            _expiryQueue.TryPeek(out int key, out DateTimeOffset expiry);

            // 2 scenarios for this key.
            // 1. This key is present in the Map and List
            if (_map.TryGetValue(key, out Node? currentNode))
            {
                // 1. This key was updated with a new expiry using Put.
                //    So this is a stale key in the queue. Delete it
                if (currentNode.ExpiresAt != expiry)
                {
                    _expiryQueue.Dequeue();
                    continue;
                }
                // 2. This key is still alive but has expired. Delete it.
                else if (currentNode.ExpiresAt < DateTimeOffset.UtcNow)
                {
                    _expiryQueue.Dequeue();
                    _map.Remove(key);
                    _list.RemoveNode(currentNode);
                    _size--;
                }
                // 3. This key is still alive and has not expired. So no other 
                //    entries will be expired. Break;
                else
                {
                    break;
                }
            }
            // 2. This key is not present in the Map and the List
            else
            {
                //The key is in the queue but was already deleted from the map 
                // (e.g., evicted by LRU capacity or manually removed). 
                // It is an orphaned ghost entry. Discard it.
                _expiryQueue.Dequeue();
            }
        }
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
}

