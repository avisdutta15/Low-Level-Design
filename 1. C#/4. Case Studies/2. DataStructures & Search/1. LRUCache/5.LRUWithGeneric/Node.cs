namespace _5.LRUWithGeneric;
public class Node<TKey, TValue>
{
    public TKey Key;
    public TValue Value;
    public Node<TKey, TValue>? Prev;
    public Node<TKey, TValue>? Next;
    public DateTimeOffset ExpiresAt;

    public Node(TKey key, TValue value, DateTimeOffset expiresAt)
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