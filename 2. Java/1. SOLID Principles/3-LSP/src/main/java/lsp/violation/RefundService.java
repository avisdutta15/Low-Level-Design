package lsp.violation;

import java.util.List;

/**
 * LSP Violation Demo: RefundService assumes all PaymentGateway instances
 * can process refunds. CryptoGateway breaks this assumption at runtime.
 */
public class RefundService {

    public void processRefunds(List<PaymentGateway> gateways) {
        for (PaymentGateway gateway : gateways) {
            // This will crash when CryptoGateway is encountered!
            gateway.refund("TXN-123", 50.0);
        }
    }

    public static void main(String[] args) {
        System.out.println("=== LSP Violation: CryptoGateway breaks substitutability ===\n");

        List<PaymentGateway> gateways = List.of(
                new StripeGateway(),
                new PayPalGateway(),
                new CryptoGateway()  // This will crash!
        );

        RefundService refundService = new RefundService();
        try {
            refundService.processRefunds(gateways);
        } catch (UnsupportedOperationException e) {
            System.out.println("\n[CRASH] " + e.getMessage());
            System.out.println("=> CryptoGateway violates LSP — it cannot be substituted for PaymentGateway in refund scenarios.");
        }
    }
}
