package isp.violation;

/**
 * ISP VIOLATION: UPI cannot support subscriptions or one-click checkout,
 * but is forced to implement them — throwing exceptions at runtime.
 */
public class UpiProvider implements PaymentProvider {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "UPI-" + System.currentTimeMillis();
        System.out.println("[UPI] Charged $" + amount + " via UPI ID " + paymentDetails + " | Txn: " + txnId);
        return txnId;
    }

    @Override
    public void refund(String txnId, double amount) {
        System.out.println("[UPI] Refunded $" + amount + " for " + txnId);
    }

    @Override
    public String createSubscription(String customerId, double monthlyAmount, String plan) {
        throw new UnsupportedOperationException("UPI does not support subscriptions!");
    }

    @Override
    public String oneClickCheckout(String customerId, String savedToken, double amount) {
        throw new UnsupportedOperationException("UPI does not support one-click checkout!");
    }

    public static void main(String[] args) {
        System.out.println("=== ISP Violation: UPI forced to implement unsupported methods ===\n");

        UpiProvider upi = new UpiProvider();
        upi.charge("CUST-001", 100.0, "user@upi");
        upi.refund("UPI-123", 25.0);

        System.out.println();
        try {
            upi.createSubscription("CUST-001", 9.99, "premium");
        } catch (UnsupportedOperationException e) {
            System.out.println("[CRASH] " + e.getMessage());
        }

        try {
            upi.oneClickCheckout("CUST-001", "token-abc", 50.0);
        } catch (UnsupportedOperationException e) {
            System.out.println("[CRASH] " + e.getMessage());
        }

        System.out.println("\n=> Fat interface forces UpiProvider to lie about its capabilities.");
    }
}
