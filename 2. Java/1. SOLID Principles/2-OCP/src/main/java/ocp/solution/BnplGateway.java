package ocp.solution;

public class BnplGateway extends PaymentGatewayBase {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        if (!performFraudCheck(customerId, amount)) {
            return "DECLINED";
        }
        String txnId = "BNPL-" + System.currentTimeMillis();
        double installment = amount / 4;
        System.out.println("[BNPL] Approved Buy-Now-Pay-Later for $" + amount
                + " (4 x $" + installment + ") | Account: " + paymentDetails
                + " | Txn: " + txnId);
        return txnId;
    }
}
