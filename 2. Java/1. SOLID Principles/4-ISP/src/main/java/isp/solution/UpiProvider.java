package isp.solution;

/**
 * ISP Solution: UPI only implements the interfaces it truly supports.
 * No dummy methods, no exceptions — clean and honest.
 */
public class UpiProvider implements Chargeable, Refundable {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "UPI-" + System.currentTimeMillis();
        System.out.println("[UPI] Charged $" + amount + " via UPI ID " + paymentDetails + " | Txn: " + txnId);
        return txnId;
    }

    @Override
    public void refund(String txnId, double amount) {
        System.out.println("[UPI] Refunded $" + amount + " for " + txnId);
    }
}
