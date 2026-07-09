public class PaymentStrategyFactory
{
    public static IPaymentStrategy GetStrategy(PaymentMode paymentMode)
    {
        switch (paymentMode)
        {
            case PaymentMode.CARD:
                return new CardPayment();
            case PaymentMode.UPI:
                return new UPIPayment();
            case PaymentMode.CASH:
                return new CashPayment();
            default:
                throw new ArgumentException("Invalid payment mode");
        }
    }
}