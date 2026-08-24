package isp.solution;

public interface SubscriptionBilling {
    String createSubscription(String customerId, double monthlyAmount, String plan);
}
