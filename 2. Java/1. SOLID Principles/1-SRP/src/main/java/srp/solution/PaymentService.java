package srp.solution;

/**
 * SRP Solution: PaymentService is now a thin orchestrator.
 * Each responsibility is delegated to a focused collaborator.
 */
public class PaymentService {

    private final PaymentValidator validator;
    private final PaymentGateway gateway;
    private final TransactionRepository repository;
    private final PaymentLogger logger;
    private final PaymentNotifier notifier;

    public PaymentService(PaymentValidator validator,
                          PaymentGateway gateway,
                          TransactionRepository repository,
                          PaymentLogger logger,
                          PaymentNotifier notifier) {
        this.validator = validator;
        this.gateway = gateway;
        this.repository = repository;
        this.logger = logger;
        this.notifier = notifier;
    }

    public void processPayment(String customerId, double amount, String cardNumber) {
        if (!validator.validate(amount, cardNumber)) {
            return;
        }
        String txnId = gateway.charge(customerId, amount, cardNumber);
        repository.save(txnId, customerId, amount);
        logger.log(txnId, customerId, amount);
        notifier.sendReceipt(customerId, txnId, amount);
    }

    public static void main(String[] args) {
        System.out.println("=== SRP Solution: Each class has ONE responsibility ===\n");

        PaymentService service = new PaymentService(
                new CardPaymentValidator(),
                new StripeGateway(),
                new DatabaseTransactionRepository(),
                new FilePaymentLogger(),
                new EmailPaymentNotifier()
        );

        service.processPayment("CUST-001", 149.99, "4111111111111111");
        System.out.println();
        service.processPayment("CUST-002", -5.0, "4111111111111111");
    }
}
