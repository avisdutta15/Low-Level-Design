package ocp.violation;

/**
 * OCP VIOLATION: Adding a new payment method requires modifying this class.
 * The if-else chain grows with every new payment type.
 */
public class PaymentGateway {

    enum PaymentMethod {
        CREDIT_CARD, PAYPAL, UPI
    }

    public String charge(String customerId, double amount, String paymentDetails, PaymentMethod method) {
        String txnId = "TXN-" + System.currentTimeMillis();

        if (method == PaymentMethod.CREDIT_CARD) {
            System.out.println("[CREDIT CARD] Charging card " + paymentDetails.substring(0, 4)
                    + "**** for $" + amount);
        } else if (method == PaymentMethod.PAYPAL) {
            System.out.println("[PAYPAL] Charging PayPal account " + paymentDetails
                    + " for $" + amount);
        } else if (method == PaymentMethod.UPI) {
            System.out.println("[UPI] Charging UPI ID " + paymentDetails
                    + " for $" + amount);
        }
        // If we add CRYPTO, BNPL etc., we MUST modify this class — violates OCP!

        System.out.println("[GATEWAY] Transaction " + txnId + " completed for customer " + customerId);
        return txnId;
    }

    public static void main(String[] args) {
        System.out.println("=== OCP Violation: Must modify PaymentGateway for every new method ===\n");

        PaymentGateway gateway = new PaymentGateway();
        gateway.charge("CUST-001", 100.0, "4111111111111111", PaymentMethod.CREDIT_CARD);
        System.out.println();
        gateway.charge("CUST-002", 75.50, "user@paypal.com", PaymentMethod.PAYPAL);
        System.out.println();
        gateway.charge("CUST-003", 200.0, "user@upi", PaymentMethod.UPI);
    }
}
