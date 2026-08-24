package lsp.solution;

import java.util.List;

/**
 * LSP Solution: RefundService only accepts List<Refundable>.
 * CryptoGateway cannot even be passed here — compile-time safety!
 */
public class RefundService {

    public void processRefunds(List<Refundable> refundableGateways) {
        for (Refundable gateway : refundableGateways) {
            gateway.refund("TXN-456", 50.0);
        }
    }

    public static void main(String[] args) {
        System.out.println("=== LSP Solution: Compile-time safety via proper abstractions ===\n");

        // Only refundable gateways can be in this list
        List<Refundable> refundableGateways = List.of(
                new StripeGateway(),
                new PayPalGateway()
        );

        RefundService refundService = new RefundService();
        refundService.processRefunds(refundableGateways);

        System.out.println("\n[INFO] CryptoGateway is NOT Refundable, so it cannot be added to this list.");
        System.out.println("[INFO] This is enforced at COMPILE TIME — no runtime surprises!");

        // The following would cause a COMPILE ERROR (uncomment to verify):
        // List<Refundable> broken = List.of(new CryptoGateway()); // ERROR!
    }
}
