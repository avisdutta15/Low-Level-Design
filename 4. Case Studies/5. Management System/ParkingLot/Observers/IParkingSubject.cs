public interface IParkingSubject
{
    void Subscribe(IParkingObserver observer);
    void Unsubscribe(IParkingObserver observer);
    void Notify();
}
