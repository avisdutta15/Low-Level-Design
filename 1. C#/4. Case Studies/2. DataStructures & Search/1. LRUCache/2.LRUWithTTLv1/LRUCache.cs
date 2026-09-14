namespace _2.LRUWithTTLv1;

public class LRUCache
{
    public Dictionary<int, Node> _map;
    public DoublyLinkedList _list;
    public int _capacity;
    public int _size;

    public LRUCache(int capacity)
    {
        _map = new Dictionary<int, Node>();
        _list = new DoublyLinkedList();
        _capacity = capacity;
        _size = 0;
    }

    public string Get(int key)
    {
        // check if the key exists in the hashtable.
        //      if not then return -1
        // if yes then get the node from the hash table
        // since this node is accessed, we need to move it to the head of the list
        // return the node's value
        if (_map.TryGetValue(key, out Node? node) == false)
        {
            return "null";
        }

        // check if the node has expired
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

        if (_map.TryGetValue(key, out Node? node) != false)
        {
            // Promote to MRU
            _list.RemoveNode(node);

            // Update the nodes values 
            node.Value = value;
            node.ExpiresAt = expiresAt;

            //Promote to MRU
            _list.AddFirst(node);
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

    public void LRUEvict()
    {
        Node lastNode = _list.Last;
        if (lastNode != _list.head) // safety: don't remove the dummy head
        {
            _list.RemoveNode(lastNode);
            _map.Remove(lastNode.Key);
            _size--;
        }
    }
}
