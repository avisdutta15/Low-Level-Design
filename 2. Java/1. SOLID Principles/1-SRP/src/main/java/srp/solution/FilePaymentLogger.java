package srp.solution;

public class FilePaymentLogger implements PaymentLogger {

    @Override
    public void log(String txnId, String customerId, double amount) {
        System.out.println("[FILE LOG] " + java.time.LocalDateTime.now()
                + " | Txn: " + txnId + " | Customer: " + customerId + " | $" + amount);
    }
}
