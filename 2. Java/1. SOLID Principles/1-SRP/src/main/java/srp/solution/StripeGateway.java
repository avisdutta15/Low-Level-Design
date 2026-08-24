package srp.solution;

public class StripeGateway implements PaymentGateway {

    @Override
    public String charge(String customerId, double amount, String cardNumber) {
        String txnId = "TXN-" + System.currentTimeMillis();
        System.out.println("[STRIPE] Charged $" + amount + " on card "
                + cardNumber.substring(0, 4) + "**** for customer " + customerId
                + " -> " + txnId);
        return txnId;
    }
}
