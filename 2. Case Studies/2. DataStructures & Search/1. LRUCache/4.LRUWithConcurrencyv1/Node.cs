namespace _4.LRUWithConcurrencyv1;
public class Node
{
    public int Key;
    public string Value;
    public Node? Prev;
    public Node? Next;
    public DateTimeOffset ExpiresAt;

    public Node(int key, string value, DateTimeOffset expiresAt)
    {
        Key = key;
        Value = value;
        Prev = null;
        Next = null;
        ExpiresAt = expiresAt;
    }

    public bool HasExpired()
    {
        return DateTimeOffset.UtcNow > ExpiresAt;
    }
}