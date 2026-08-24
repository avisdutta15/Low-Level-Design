package realworld;

public class PayPalProcessor implements PaymentProcessor {
    @Override
    public boolean processPayment(Order order, PaymentDetails payment) {
        System.out.println("Redirecting to PayPal for order " + order.getOrderId() + "...");
        return true;
    }
}
