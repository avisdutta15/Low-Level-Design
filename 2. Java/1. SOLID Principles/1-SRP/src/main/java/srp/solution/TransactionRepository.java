package srp.solution;

/**
 * Single responsibility: persist transaction data.
 */
public interface TransactionRepository {
    void save(String txnId, String customerId, double amount);
}
