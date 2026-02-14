public interface PaymentProcessor
{
    void Pay(double amountInUSD);
}

public class StripeClient
{
    public void MakePayment(string cardNumber, double amountInUSD)
    {
        Console.WriteLine($"Stripe: Recieved payment from : {amountInUSD} with card number : {cardNumber}");
    }
}

public class RazorpayClient
{
    public void CreatePayment(string phoneNumber, double amountInPaise)
    {
        Console.WriteLine($"Stripe: Recieved payment from : {amountInPaise} from phone number : {phoneNumber}");
    }
}

public class StripeAdapter : PaymentProcessor
{
    private StripeClient? _client = null;
    private string cardNumber;

    public StripeAdapter(string cardNumber)
    {
        // Stripe.apiKey = "sk_test_...";
        _client = new StripeClient();
        this.cardNumber = cardNumber;
    }
    public void Pay(double amountInUSD)
    {
        _client!.MakePayment(cardNumber, amountInUSD);
        Console.WriteLine("Stripe: Paid $" + amountInUSD + " using card " + cardNumber);
    }
}

public class RazorpayAdapter : PaymentProcessor
{
    private RazorpayClient _client;
    private string phoneNumber;
    private static double USD_TO_INR = 87.0;

    public RazorpayAdapter(string phoneNumber)
    {
        _client = new RazorpayClient();
        this.phoneNumber = phoneNumber;
    }
    
    public void Pay(double amountInUSD)
    {
        double amountInINR = amountInUSD * USD_TO_INR;
        int amountInPaise = (int)(amountInINR * 100);
        _client.CreatePayment(phoneNumber, amountInPaise);
        Console.WriteLine("Razorpay: Paid " + amountInPaise / 100.0 + " Rs using phone number " + phoneNumber);
    }
}

public class PaymentAdapter
{
    public static void Main()
    {
        string selectedMethod = "razorpay";

        PaymentProcessor? processor = null;

        // we can use factory design pattern for creating adapter based on user selection
        switch (selectedMethod.ToLower())
        {
            case "stripe":
                processor = new StripeAdapter("4111-1111-1111-1111");
                break;
            case "razorpay":
                processor = new RazorpayAdapter("9876543210");
                break;
            default:
                throw new ArgumentException("Invalid payment method selected");
        }

        // here, we used strategy design pattern since at runtime pay() will behave differently
        // we also used adapter design pattern so that we can have unified pay() method and talk with 3rd party api
        processor.Pay(50.0);
    }
}


/*
 * Adapter Pattern:
 * ----------------
 * Adapter is a structural design pattern that allows objects with incompatible interfaces (methods) to collaborate.
 * 
 * Example:
 * --------
 * We have two payment gateways:
 * Stripe : Cheap for USA members. SDK provides method like 
 *          MakePayment(cardNumber, amountInUSD)
 * Razorpay:Cheap for Indian members. It provides API like
 *          CreatePayment(phoneNumber, amountInPaise)
 * 
 * This means :
 * 1. We need if-else check every where to decide which payment gateway to use
 * 2. Third party APIs can change their names or signatures anytime which would break the code.
 * 
 * To solve this:
 * We create Adapters around both the payment gatways by incorporating an unified contract - Interface (pay)
 * Now when we need to pay, we just select the payment gateway and make the payment using the payment processor,
 * The PaymentProcessor is implemented by both the Adapters. The Adapters have pay method which the client calls,
 * The Pay method in individaul Adapter calls the respective Gatways' API (different name/ signature) to make payment.
 * 
 * Here StripeClient is the Adaptee and StripeAdapter is the adapter.
 * 
 * Type of Adapter
 * ---------------
 * 1. Class Adapter (using inheritance)
 *    public class LeagayWeatherAdapter : LegacyWeatherService, IModernWeatherService 
 *    {
 *      // Inherits from legacy and implements modern interface
 *      public WeatherData GetWeatherData(string city, string countryCode)
 *      {
 *          string xml = base.FetchWeather(city);
 *          return ParseXmlToWeatherData(xml);
 *      }
 *      // ... other methods
 *    }
 *    
 *    In Class Adapter, the adapter inherits from Adaptee.
 *    
 * 2. Object Adpter (using composition)
 *    In Object Adapter, the adaptee doesnot inherit from adaptee but instead holds an instance of it.
 *    public class RazorpayAdapter : PaymentProcessor
 *    {
 *      private RazorpayClient _client; // composition
 *    }
 *  
 *  Use Adapter when:
 *  -----------------
 *  ✅ You need to make incompatible classes work together
 *  ✅ You want to reuse existing classes with different interfaces
 *  ✅ You're integrating third-party libraries
 *  ✅ You need to provide a stable interface to changing implementations
 *  
 *  
 *  LLD Usecase:
 *  ------------
 *  "Integrate a legacy database system with a modern application"
 *  "Make two different payment gateways work with your e-commerce system"
 *  "Wrap a third-party API to match your application's interface"
 *  
 *  
 *  
 *  Strategy
 *  "Design a navigation system that can use different route algorithms (fastest, shortest, scenic)"
 *  "Implement a compression utility that supports different algorithms (ZIP, RAR, 7Z)"
 *  "Create a sorting system that can use different sort algorithms"
 */