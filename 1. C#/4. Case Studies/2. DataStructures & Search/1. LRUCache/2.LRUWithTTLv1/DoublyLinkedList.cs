namespace _2.LRUWithTTLv1;

public class DoublyLinkedList
{
    public Node head;
    public Node tail;

    public DoublyLinkedList()
    {
        //create dummy head and tail
        head = new Node(-1, "head", DateTimeOffset.MaxValue);
        tail = new Node(-1, "tail", DateTimeOffset.MaxValue);
        head.Next = tail;
        tail.Prev = head;
    }

    public Node Last { get => tail?.Prev!; }
    public Node First { get => head?.Next!; }

    public void AddFirst(Node node)
    {
        Node headNext = head.Next!;

        //link node and head next
        headNext.Prev = node;
        node.Next = headNext;

        //link node and head
        node.Prev = head;
        head.Next = node;
    }

    public void RemoveNode(Node node)
    {
        Node nodeNext = node.Next!;
        Node nodePrev = node.Prev!;

        //link nodenext and nodeprev
        nodePrev.Next = nodeNext;
        nodeNext.Prev = nodePrev;

        //make prev and next of node as null
        node.Next = null;
        node.Prev = null;
    }
}
