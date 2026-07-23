public class Account
{
    public int AccountNumber { get; set; }
    public int AvailableBalance { get; set; }
}

public class Card
{
    public string CardNumber { get; set; }
    public string PinNumber { get; set; }
    public Account Account { get; set; }
    public Card()
    {

    }
    public Card(string cardNumber, string pin, Account account)
    {
        CardNumber = cardNumber;
        PinNumber = pin;
        Account = account;
    }
}

public class ATM
{
    public string Id { get; set; }
    public ATMSTATUS Status { get; set; }

    //This is a calculated read-only view.
    public int CashAvailable
    {
        get
        {
            return (TwoThousandCount * 2000) + (FiveHundredCount * 500) + (OneHundredCount * 100);
        }
    }

    public int TwoThousandCount { get; set; }
    public int FiveHundredCount { get; set; }
    public int OneHundredCount { get; set; }

    public ATM(string id, int twoThousandCount, int fiveHundredCount, int oneHundredCount)
    {
        Id = id;
        Status = ATMSTATUS.IDLE;            //initially the atm is in IDLE state
        TwoThousandCount = twoThousandCount;
        FiveHundredCount = fiveHundredCount;
        OneHundredCount = oneHundredCount;
    }

    public bool DeductBalace(int amount)
    {
        // 1. Calculate how many bills we CAN take (Greedy approach)
        // We use Math.Min to ensure we don't take more bills than we actually have.
        int required2000 = Math.Min(TwoThousandCount, amount / 2000);
        int remainingAmount = amount - (required2000 * 2000);

        int required500 = Math.Min(FiveHundredCount, remainingAmount / 500);
        remainingAmount = remainingAmount - (required500 * 500);

        int required100 = Math.Min(OneHundredCount, remainingAmount / 100);
        remainingAmount = remainingAmount - (required100 * 100);

        // 2. If remainingAmount is 0, it means we found a valid combination of bills.
        // Now we can physically update the state.
        if (remainingAmount == 0)
        {
            TwoThousandCount -= required2000;
            FiveHundredCount -= required500;
            OneHundredCount -= required100;
            return true; // Success
        }
        return false;   // Failed to dispense exact amount (e.g., asked for 50 rs or insufficient notes)
    }
}

public enum ATMSTATUS
{
    IDLE,
    CARD_INSERTED,
    AUTHENTICATED,
    DISPENSE_CASH
}

public class ATMRepository
{
    private Dictionary<string, ATM> _atms = new();
    public void AddATM(ATM atm) => _atms.Add(atm.Id, atm);
    public ATM? GetById(string atmId)
    {
        _atms.TryGetValue(atmId, out ATM? atm);
        return atm;
    }
    public void updateATMStatus(ATM atm, ATMSTATUS status)
    {
        if (_atms.TryGetValue(atm.Id, out ATM? atmResult))
        {
            atmResult.Status = status;
        }
    }
}

//States of ATMMachine
public interface IATMState
{
    public void InsertCard(ATMMAchine atmMachine, Card card);
    public void EnterPin(ATMMAchine atmMachine, string pin);
    public void SelectionOption(ATMMAchine atmMachine, string option);
    public void DispenseCash(ATMMAchine atmMachine, int amount);
    public void EjectCard(ATMMAchine atmMachine);
}

//Concreate States
// --- State: IDLE. Actions Allowed: InsertCard ---
public class IdleState : IATMState
{
    public void DispenseCash(ATMMAchine atmMachine, int amount) => Console.WriteLine("No card inserted.");
    public void EjectCard(ATMMAchine atmMachine) => Console.WriteLine("No card inserted.");
    public void EnterPin(ATMMAchine atmMachine, string pin) => Console.WriteLine("No card inserted.");
    public void InsertCard(ATMMAchine atmMachine, Card card)
    {
        atmMachine.SetCard(card);
        Console.WriteLine("Card Inserted.");
        atmMachine.SetState(new CardInsertedState());
    }
    public void SelectionOption(ATMMAchine atmMachine, string option) => Console.WriteLine("No card inserted.");
}

// --- State: CARD INSERTED. Actions Allowed: EjectCard and EnterPin ---
public class CardInsertedState : IATMState
{
    public void DispenseCash(ATMMAchine atmMachine, int amount) => Console.WriteLine("Error: Enter PIN first.");
    public void EjectCard(ATMMAchine atmMachine)
    {
        Console.WriteLine("Card Ejected.");
        atmMachine.SetCard(null);
        atmMachine.SetState(new IdleState());
    }
    public void EnterPin(ATMMAchine atmMachine, string pin)
    {
        if(atmMachine.CurrentCard!.PinNumber == pin)
        {
            Console.WriteLine("PIN Correct. Authenticated.");
            atmMachine.SetState(new AuthenticatedState());
        }
        else
        {
            Console.WriteLine("Error: Incorrect PIN.");
            atmMachine.EjectCard();
        }
    }
    public void InsertCard(ATMMAchine atmMachine, Card card) => Console.WriteLine("Error: Card already inserted.");
    public void SelectionOption(ATMMAchine atmMachine, string option) => Console.WriteLine("Error: Enter PIN first.");
}

// --- State: AUTHENTICATED. Actions Allowed: EjectCard and SelectOption ---
public class AuthenticatedState : IATMState
{
    public void DispenseCash(ATMMAchine atmMachine, int amount) => Console.WriteLine("Error: Select option first.");
    public void EjectCard(ATMMAchine atmMachine)
    {
        Console.WriteLine("Card Ejected.");
        atmMachine.SetCard(null);
        atmMachine.SetState(new IdleState());
    }
    public void EnterPin(ATMMAchine atmMachine, string pin) => Console.WriteLine("Error: Already authenticated.");
    public void InsertCard(ATMMAchine atmMachine, Card card) => Console.WriteLine("Error: Card already inserted.");
    public void SelectionOption(ATMMAchine atmMachine, string option)
    {
        Console.WriteLine($"Option {option} Selected.");
        if (option == "WITHDRAW")
        {
            atmMachine.SetState(new DispenseCashState());
        }
    }
}

// --- State: DISPENSE CASH. Actions Allowed: EjectCard and DispenseCash ---
public class DispenseCashState : IATMState
{
    public void DispenseCash(ATMMAchine atmMachine, int amount)
    {
        if (atmMachine.CurrentCard!.Account.AvailableBalance < amount)
        {
            Console.WriteLine("Insufficient Account Balance");
            atmMachine.EjectCard();
            return;
        }
        
        //else the account has balance. Now check if the atm has balance / suitable denomination
        bool success = atmMachine.atm.DeductBalace(amount);
        if (success)
        {
            atmMachine.CurrentCard!.Account.AvailableBalance = atmMachine.CurrentCard.Account.AvailableBalance - amount;
            Console.WriteLine($"Dispensing {amount}. New Balance: {atmMachine.CurrentCard.Account.AvailableBalance}");
            atmMachine.EjectCard();
        }
        else
        {
            Console.WriteLine("Error: ATM Insufficient Cash or No Suitable Denominations.");
            atmMachine.EjectCard();
        }
    }
    public void EjectCard(ATMMAchine atmMachine)
    {
        Console.WriteLine("Card Ejected.");
        atmMachine.SetCard(null);
        atmMachine.SetState(new IdleState());
    }
    public void EnterPin(ATMMAchine atmMachine, string pin) => Console.WriteLine("Error: Already authenticated.");
    public void InsertCard(ATMMAchine atmMachine, Card card) => Console.WriteLine("Error: Card already inserted.");
    public void SelectionOption(ATMMAchine atmMachine, string option) => Console.WriteLine("Error: Transaction in progress.");
}

//Used by the Users to access the atm
public class ATMMAchine
{
    public ATM atm { get; }
    private readonly ATMRepository _repository;

    // We need to hold the card internally while processing
    public Card? CurrentCard { get; private set; }
    private IATMState _currentState;

    public ATMMAchine(ATM atm, ATMRepository repository)
    {
        this.atm = atm;
        _repository = repository;
        _currentState = new IdleState(); // Initial State
    }

    //The following functions are exposed by the an ATM Machine
    public void InsertCard(Card card) => _currentState.InsertCard(this, card);
    public void EnterPin(string pin) => _currentState.EnterPin(this, pin);
    public void SelectionOption(string option) => _currentState.SelectionOption(this, option);
    public void DispenseCash(int amount) => _currentState.DispenseCash(this, amount);
    public void EjectCard() => _currentState.EjectCard(this);

    //State manipulation method
    public void SetCard(Card card)
    {
        CurrentCard = card;
    }

    public void SetState(IATMState newState)
    {
        _currentState = newState;

        // Map the State Class to the Enum
        ATMSTATUS newStatus = newState switch
        {
            IdleState => ATMSTATUS.IDLE,
            CardInsertedState => ATMSTATUS.CARD_INSERTED,
            AuthenticatedState => ATMSTATUS.AUTHENTICATED,
            DispenseCashState => ATMSTATUS.DISPENSE_CASH,
            _ => ATMSTATUS.IDLE
        };

        atm.Status = newStatus;
        _repository.updateATMStatus(atm, newStatus);
    }
}

//Used by the Admin to add ATMs
public class ATMService
{
    private ATMRepository _atmRepository;
    public ATMService(ATMRepository atmRepository)
    {
        _atmRepository = atmRepository;
    }
    public void AddATM(ATM atm)
    {
        _atmRepository.AddATM(atm);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        //first create an account of the user and create a card that refers the account
        Card card = new Card
        {
            CardNumber = "100010",
            PinNumber = "123",
            Account = new Account()
            {
                AccountNumber = 1234,
                AvailableBalance = 2500
            }
        };

        //create atms they hold the atm_name, 2Thousand, 1Thousand, 5Hundred ruppes
        ATM atm1 = new ATM("ATM1", 5, 5, 20);
        ATM atm2 = new ATM("ATM2", 0, 2, 5);

        //add them to the system
        ATMRepository repository = new ATMRepository();
        ATMService atmService = new ATMService(repository);
        atmService.AddATM(atm1);
        atmService.AddATM(atm2);

        //now on a particular atm machine, do the following actions:
        ATMMAchine atmMachine = new ATMMAchine(atm1, repository);

        // 1. atmmachine1.insert_card(card)
        atmMachine.InsertCard(card);

        // 2. atmmachine1.enter_pin("123");
        atmMachine.EnterPin("123");

        // 3. atmmachine1.select_option("WITHDRAW");
        atmMachine.SelectionOption("WITHDRAW");

        // 4. atmmachine1.dispense_cash(1000);
        atmMachine.DispenseCash(1000);
    }
}