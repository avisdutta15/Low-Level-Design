public class CardPayment : IPaymentStrategy
{
    public bool Pay(Ticket ticket, double amount)
    {
        Console.WriteLine($"Paid ₹{amount} for ticket {ticket.Id} via Card.");
        return true;
    }
}