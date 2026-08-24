package srp.solution;

public class DatabaseTransactionRepository implements TransactionRepository {

    @Override
    public void save(String txnId, String customerId, double amount) {
        System.out.println("[DATABASE] Saved transaction " + txnId
                + " | Customer: " + customerId + " | Amount: $" + amount);
    }
}
