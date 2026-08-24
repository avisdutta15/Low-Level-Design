package realworld;

// LSP: Can be substituted for any PaymentProcessor without breaking anything
public class CreditCardProcessor implements PaymentProcessor {
    @Override
    public boolean processPayment(Order order, PaymentDetails payment) {
        System.out.println("Charging credit card for order " + order.getOrderId() + "...");
        return true;
    }
}
