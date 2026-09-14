namespace _5.LRUWithGeneric;

public class DoublyLinkedList<TKey, TValue>
{
    public Node<TKey, TValue> head;
    public Node<TKey, TValue> tail;

    public DoublyLinkedList()
    {
        //create dummy head and tail
        head = new Node<TKey, TValue>(default(TKey)!, default(TValue)!, DateTimeOffset.MaxValue);
        tail = new Node<TKey, TValue>(default(TKey)!, default(TValue)!, DateTimeOffset.MaxValue);
        head.Next = tail;
        tail.Prev = head;
    }

    public Node<TKey, TValue> Last { get => tail?.Prev!; }
    public Node<TKey, TValue> First { get => head?.Next!; }

    public void AddFirst(Node<TKey, TValue> node)
    {
        Node<TKey, TValue> headNext = head.Next!;

        //link node and head next
        headNext.Prev = node;
        node.Next = headNext;

        //link node and head
        node.Prev = head;
        head.Next = node;
    }

    public void RemoveNode(Node<TKey, TValue> node)
    {
        Node<TKey, TValue> nodeNext = node.Next!;
        Node<TKey, TValue> nodePrev = node.Prev!;

        //link nodenext and nodeprev
        nodePrev.Next = nodeNext;
        nodeNext.Prev = nodePrev;

        //make prev and next of node as null
        node.Next = null;
        node.Prev = null;
    }
}
