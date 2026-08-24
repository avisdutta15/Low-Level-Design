package realworld;

public class PaymentDetails {
    private String cardNumber;
    private String paymentMethod;

    public PaymentDetails(String cardNumber, String paymentMethod) {
        this.cardNumber = cardNumber;
        this.paymentMethod = paymentMethod;
    }

    public String getCardNumber() {
        return cardNumber;
    }

    public String getPaymentMethod() {
        return paymentMethod;
    }
}
