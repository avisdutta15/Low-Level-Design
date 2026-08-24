package srp.solution;

public class EmailPaymentNotifier implements PaymentNotifier {

    @Override
    public void sendReceipt(String customerId, String txnId, double amount) {
        System.out.println("[EMAIL] Receipt sent to " + customerId
                + " for transaction " + txnId + " | Amount: $" + amount);
    }
}
