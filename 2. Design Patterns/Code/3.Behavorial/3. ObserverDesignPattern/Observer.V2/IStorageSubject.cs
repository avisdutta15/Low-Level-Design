namespace Observer.V2;

/// <summary>
/// Subject interface — defines subscribe/unsubscribe/notify contract.
/// </summary>
public interface IStorageSubject
{
    void Subscribe(IStorageObserver observer);
    void Unsubscribe(IStorageObserver observer);
}
