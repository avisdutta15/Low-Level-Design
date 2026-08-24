package isp.solution;

public interface OneClickPayment {
    String oneClickCheckout(String customerId, String savedToken, double amount);
}
