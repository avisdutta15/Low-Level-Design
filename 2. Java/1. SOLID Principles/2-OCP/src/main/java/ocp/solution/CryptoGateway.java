package ocp.solution;

public class CryptoGateway extends PaymentGatewayBase {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        if (!performFraudCheck(customerId, amount)) {
            return "DECLINED";
        }
        String txnId = "CRYPTO-" + System.currentTimeMillis();
        System.out.println("[CRYPTO] Charged wallet " + paymentDetails
                + " for $" + amount + " | Txn: " + txnId);
        return txnId;
    }
}
