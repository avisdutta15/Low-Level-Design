package srp.solution;

/**
 * Single responsibility: notify customers about payments.
 */
public interface PaymentNotifier {
    void sendReceipt(String customerId, String txnId, double amount);
}
