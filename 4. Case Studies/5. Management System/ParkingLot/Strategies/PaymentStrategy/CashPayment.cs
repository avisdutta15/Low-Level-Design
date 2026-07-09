public class CashPayment : IPaymentStrategy
{
    public bool Pay(Ticket ticket, double amount)
    {
        Console.WriteLine($"Paid ₹{amount} for ticket {ticket.Id} via Cash.");
        return true;
    }
}