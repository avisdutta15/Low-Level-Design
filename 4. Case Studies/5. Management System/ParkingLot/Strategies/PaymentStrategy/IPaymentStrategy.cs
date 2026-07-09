public interface IPaymentStrategy
{
    public bool Pay(Ticket ticket, double amount);
}