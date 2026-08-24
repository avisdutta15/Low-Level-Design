package isp.solution;

/**
 * ISP Solution: Stripe supports all capabilities,
 * so it implements all four interfaces.
 */
public class StripeProvider implements Chargeable, Refundable, SubscriptionBilling, OneClickPayment {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "STRIPE-" + System.currentTimeMillis();
        System.out.println("[STRIPE] Charged $" + amount + " | Txn: " + txnId);
        return txnId;
    }

    @Override
    public void refund(String txnId, double amount) {
        System.out.println("[STRIPE] Refunded $" + amount + " for " + txnId);
    }

    @Override
    public String createSubscription(String customerId, double monthlyAmount, String plan) {
        String subId = "SUB-" + System.currentTimeMillis();
        System.out.println("[STRIPE] Subscription: " + plan + " @ $" + monthlyAmount + "/mo | " + subId);
        return subId;
    }

    @Override
    public String oneClickCheckout(String customerId, String savedToken, double amount) {
        String txnId = "1CLICK-" + System.currentTimeMillis();
        System.out.println("[STRIPE] One-click: $" + amount + " with token " + savedToken + " | " + txnId);
        return txnId;
    }
}
