public class UPIPayment : IPaymentStrategy
{
    public bool Pay(Ticket ticket, double amount)
    {
        Console.WriteLine($"Paid ₹{amount} for ticket {ticket.Id} via UPI.");
        return true;
    }
}