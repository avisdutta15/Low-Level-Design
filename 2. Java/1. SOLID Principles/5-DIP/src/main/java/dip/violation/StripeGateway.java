package dip.violation;

/**
 * Concrete low-level class.
 */
public class StripeGateway {

    public String charge(String customerId, double amount, String cardNumber) {
        String txnId = "STRIPE-" + System.currentTimeMillis();
        System.out.println("[STRIPE] Charged card " + cardNumber.substring(0, 4)
                + "**** for $" + amount + " | Txn: " + txnId);
        return txnId;
    }

    public void refund(String txnId, double amount) {
        System.out.println("[STRIPE] Refunded $" + amount + " for " + txnId);
    }
}
