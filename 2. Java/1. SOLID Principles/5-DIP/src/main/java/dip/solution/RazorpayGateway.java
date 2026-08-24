package dip.solution;

public class RazorpayGateway implements Chargeable, Refundable {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "RZPAY-" + System.currentTimeMillis();
        System.out.println("[RAZORPAY] Charged via " + paymentDetails
                + " for $" + amount + " | Txn: " + txnId);
        return txnId;
    }

    @Override
    public void refund(String txnId, double amount) {
        System.out.println("[RAZORPAY] Refunded $" + amount + " for " + txnId);
    }
}
