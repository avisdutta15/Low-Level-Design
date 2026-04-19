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
    public int CashAvailable { 
        get 
        {
            return (TwoThousandCount*2000) + (FiveHundredCount*500) + (OneHundredCount*100);
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
        int required2000 = Math.Min(TwoThousandCount, amount/2000);
        int remainingAmount = amount - (required2000 * 2000);

        int required500 = Math.Min(FiveHundredCount, remainingAmount/500);
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
        if(_atms.TryGetValue(atm.Id, out ATM? atmResult))
        {
            atmResult.Status = status;
        }
    }
}

//Used by the Users to access the atm
public class ATMMAchine
{
    private readonly ATM _atm;
    private readonly ATMRepository _repository;

    // We need to hold the card internally while processing
    private Card? _currentCard;

    public ATMMAchine(ATM atm, ATMRepository repository)
    {
        _atm = atm;
        _repository = repository;
    }

    //The following functions are exposed by the an ATM Machine
    public void InsertCard(Card card) 
    {
        if(_atm.Status == ATMSTATUS.IDLE)
        {
            Console.WriteLine("Card Inserted.");
            _currentCard = card;
            _atm.Status = ATMSTATUS.CARD_INSERTED;
            _repository.updateATMStatus(_atm, ATMSTATUS.CARD_INSERTED);
        }
        else
        {
            Console.WriteLine("Error: Please wait, operation in progress.");
        }
    }

    public void EnterPin(string pin) 
    {
        if (_atm.Status == ATMSTATUS.CARD_INSERTED) 
        {
            if (_currentCard!.PinNumber == pin) 
            {
                Console.WriteLine("PIN Correct. Authenticated.");
                _atm.Status = ATMSTATUS.AUTHENTICATED;
                _repository.updateATMStatus(_atm, ATMSTATUS.AUTHENTICATED);
            }
            else
            {
                Console.WriteLine("Error: Incorrect PIN.");
                EjectCard();
            }
        }
        else
        {
            Console.WriteLine("Error: Insert card first.");
        }
    }

    public void SelectionOption(string option) 
    {
        if (_atm.Status == ATMSTATUS.AUTHENTICATED)
        {
            Console.WriteLine($"Option {option} Selected.");
            if (option == "WITHDRAW")
            {
                _atm.Status = ATMSTATUS.DISPENSE_CASH;
                _repository.updateATMStatus(_atm, ATMSTATUS.DISPENSE_CASH);
            }
        }
        else
        {
            Console.WriteLine("Error: Please authenticate first.");
        }
    }

    public void DispenseCash(int amount)
    {
        if(_atm.Status == ATMSTATUS.DISPENSE_CASH)
        {
            if(_currentCard!.Account.AvailableBalance < amount)
            {
                Console.WriteLine("Insufficient Account Balance");
                EjectCard();
                return;
            }

            //else the account has balance. Now check if the atm has balance / suitable denomination
            bool success = _atm.DeductBalace(amount);
            if(success) 
            {
                _currentCard!.Account.AvailableBalance = _currentCard.Account.AvailableBalance - amount;
                Console.WriteLine($"Dispensing {amount}. New Balance: {_currentCard.Account.AvailableBalance}");
                EjectCard();
            }
            else
            {
                Console.WriteLine("Error: ATM Insufficient Cash or No Suitable Denominations.");
                EjectCard();
            }
        }
        else
        {
            Console.WriteLine("Error: Select an option first.");
        }
    }

    public void EjectCard()
    {
        Console.WriteLine("Card Ejected. Thank you.");
        _currentCard = null;
        _atm.Status = ATMSTATUS.IDLE;
        _repository.updateATMStatus(_atm, ATMSTATUS.IDLE);
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