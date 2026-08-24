package realworld;

// ISP: Small, focused interface
public interface PaymentProcessor {
    boolean processPayment(Order order, PaymentDetails payment);
}
