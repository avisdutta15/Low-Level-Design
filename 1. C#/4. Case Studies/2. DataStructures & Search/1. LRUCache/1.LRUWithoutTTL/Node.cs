namespace _1.LRUWithoutTTL;

public class Node
{
    public int Key;
    public string Value;
    public Node? Prev;
    public Node? Next;

    public Node(int key, string value)
    {
        Key = key;
        Value = value;
        Prev = null;
        Next = null;
    }
}
