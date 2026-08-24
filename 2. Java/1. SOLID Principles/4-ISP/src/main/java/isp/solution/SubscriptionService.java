package isp.solution;

/**
 * ISP Solution: SubscriptionService depends only on SubscriptionBilling.
 * It doesn't know or care about charge, refund, or one-click capabilities.
 */
public class SubscriptionService {

    private final SubscriptionBilling billingProvider;

    public SubscriptionService(SubscriptionBilling billingProvider) {
        this.billingProvider = billingProvider;
    }

    public void subscribe(String customerId, double monthlyAmount, String plan) {
        System.out.println("Creating subscription for " + customerId + "...");
        String subId = billingProvider.createSubscription(customerId, monthlyAmount, plan);
        System.out.println("Subscription active: " + subId + "\n");
    }

    public static void main(String[] args) {
        System.out.println("=== ISP Solution: Clients depend only on interfaces they use ===\n");

        // Stripe supports subscriptions
        SubscriptionService service = new SubscriptionService(new StripeProvider());
        service.subscribe("CUST-001", 29.99, "Pro Plan");
        service.subscribe("CUST-002", 9.99, "Basic Plan");

        // UPI does NOT implement SubscriptionBilling, so it cannot be passed here.
        // The following would be a COMPILE ERROR:
        // SubscriptionService broken = new SubscriptionService(new UpiProvider()); // ERROR!

        System.out.println("[INFO] UpiProvider cannot be used here — it doesn't implement SubscriptionBilling.");
        System.out.println("[INFO] This is enforced at COMPILE TIME, not with runtime exceptions!");
    }
}
