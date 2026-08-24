package srp.solution;

/**
 * Single responsibility: log payment activity.
 */
public interface PaymentLogger {
    void log(String txnId, String customerId, double amount);
}
