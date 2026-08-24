package dip.violation;

/**
 * DIP VIOLATION: PaymentService directly depends on the concrete StripeGateway.
 * Cannot switch to another gateway without modifying this class.
 */
public class PaymentService {

    // Tight coupling to concrete class
    private final StripeGateway gateway = new StripeGateway();

    public void processPayment(String customerId, double amount, String cardNumber) {
        System.out.println("Processing payment for " + customerId + "...");
        String txnId = gateway.charge(customerId, amount, cardNumber);
        System.out.println("Payment complete: " + txnId);
    }

    public void processRefund(String txnId, double amount) {
        System.out.println("Processing refund...");
        gateway.refund(txnId, amount);
        System.out.println("Refund complete.");
    }

    public static void main(String[] args) {
        System.out.println("=== DIP Violation: PaymentService is tightly coupled to StripeGateway ===\n");

        PaymentService service = new PaymentService();
        service.processPayment("CUST-001", 199.99, "4111111111111111");
        System.out.println();
        service.processRefund("STRIPE-123", 50.0);

        System.out.println("\n=> Cannot switch to Razorpay or any other gateway without modifying PaymentService.");
        System.out.println("=> High-level policy depends on low-level detail — violates DIP.");
    }
}
