package srp.violation;

/**
 * SRP VIOLATION: This class handles validation, charging, persistence,
 * logging, and notification — too many responsibilities in one place.
 */
public class PaymentService {

    public void processPayment(String customerId, double amount, String cardNumber) {
        // 1. Validate
        if (amount <= 0) {
            System.out.println("[VALIDATION FAILED] Amount must be positive.");
            return;
        }
        if (cardNumber == null || cardNumber.length() < 13) {
            System.out.println("[VALIDATION FAILED] Invalid card number.");
            return;
        }

        // 2. Charge the card (gateway logic)
        String txnId = "TXN-" + System.currentTimeMillis();
        System.out.println("[GATEWAY] Charging card " + cardNumber.substring(0, 4) + "****"
                + " amount $" + amount + " -> Transaction: " + txnId);

        // 3. Save to database
        System.out.println("[DATABASE] INSERT INTO transactions(txn_id, customer_id, amount) VALUES ('"
                + txnId + "', '" + customerId + "', " + amount + ")");

        // 4. Log to file
        System.out.println("[FILE LOG] " + java.time.LocalDateTime.now()
                + " | Payment processed: " + txnId + " | Customer: " + customerId + " | $" + amount);

        // 5. Send notification
        System.out.println("[EMAIL] Sending receipt to customer " + customerId
                + " for transaction " + txnId + " amount $" + amount);
    }

    public static void main(String[] args) {
        System.out.println("=== SRP Violation: PaymentService does EVERYTHING ===\n");
        PaymentService service = new PaymentService();
        service.processPayment("CUST-001", 99.99, "4111111111111111");
        System.out.println();
        service.processPayment("CUST-002", -10.0, "4111111111111111");
        System.out.println();
        service.processPayment("CUST-003", 50.0, "123");
    }
}
