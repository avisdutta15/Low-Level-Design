package dip.solution;

/**
 * DIP Solution: PaymentService depends on abstractions (Chargeable, Refundable),
 * not on concrete implementations. Gateways are injected via the constructor.
 */
public class PaymentService {

    private final Chargeable chargeGateway;
    private final Refundable refundGateway;

    public PaymentService(Chargeable chargeGateway, Refundable refundGateway) {
        this.chargeGateway = chargeGateway;
        this.refundGateway = refundGateway;
    }

    public void processPayment(String customerId, double amount, String paymentDetails) {
        System.out.println("Processing payment for " + customerId + "...");
        String txnId = chargeGateway.charge(customerId, amount, paymentDetails);
        System.out.println("Payment complete: " + txnId + "\n");
    }

    public void processRefund(String txnId, double amount) {
        System.out.println("Processing refund...");
        refundGateway.refund(txnId, amount);
        System.out.println("Refund complete.\n");
    }

    public static void main(String[] args) {
        System.out.println("=== DIP Solution: Depend on abstractions, not concretions ===\n");

        // Using Stripe
        StripeGateway stripe = new StripeGateway();
        PaymentService stripeService = new PaymentService(stripe, stripe);
        stripeService.processPayment("CUST-001", 299.99, "4111111111111111");
        stripeService.processRefund("STRIPE-001", 50.0);

        // Switching to Razorpay — NO changes to PaymentService!
        RazorpayGateway razorpay = new RazorpayGateway();
        PaymentService razorpayService = new PaymentService(razorpay, razorpay);
        razorpayService.processPayment("CUST-002", 150.0, "user@upi");
        razorpayService.processRefund("RZPAY-001", 30.0);

        System.out.println("=> Swapped gateway without touching PaymentService — DIP in action!");
    }
}
