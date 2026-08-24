package ocp.solution;

/**
 * OCP Solution: PaymentService depends on the PaymentGateway abstraction.
 * New gateways can be added without modifying existing code.
 */
public class PaymentService {

    private final PaymentGateway gateway;

    public PaymentService(PaymentGateway gateway) {
        this.gateway = gateway;
    }

    public String pay(String customerId, double amount, String paymentDetails) {
        System.out.println("Processing payment for customer " + customerId + "...");
        String txnId = gateway.charge(customerId, amount, paymentDetails);
        System.out.println("Payment complete: " + txnId + "\n");
        return txnId;
    }

    public static void main(String[] args) {
        System.out.println("=== OCP Solution: Add new gateways WITHOUT modifying existing code ===\n");

        new PaymentService(new StripeGateway())
                .pay("CUST-001", 150.0, "4111111111111111");

        new PaymentService(new PayPalGateway())
                .pay("CUST-002", 75.0, "user@paypal.com");

        new PaymentService(new UpiGateway())
                .pay("CUST-003", 200.0, "user@upi");

        new PaymentService(new CryptoGateway())
                .pay("CUST-004", 500.0, "0xABC123");

        new PaymentService(new BnplGateway())
                .pay("CUST-005", 1200.0, "bnpl-account-42");

        // Demonstrates fraud check blocking high-value crypto payment
        new PaymentService(new CryptoGateway())
                .pay("CUST-006", 15000.0, "0xSUS999");
    }
}
