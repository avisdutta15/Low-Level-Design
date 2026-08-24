package isp.violation;

/**
 * ISP VIOLATION: A fat interface that forces all providers
 * to implement methods they may not support.
 */
public interface PaymentProvider {

    String charge(String customerId, double amount, String paymentDetails);

    void refund(String txnId, double amount);

    String createSubscription(String customerId, double monthlyAmount, String plan);

    String oneClickCheckout(String customerId, String savedToken, double amount);
}
