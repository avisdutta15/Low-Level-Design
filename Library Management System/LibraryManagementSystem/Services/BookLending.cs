using LibraryManagementSystem.Utils;

namespace LibraryManagementSystem.Services
{
    public class BookLending
    {
        public DateTime CreationDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public string BookItemBarcode { get; set; }
        public string MemberId { get; set; }

        // Singleton pattern
        private static BookLending _instance;
        private static readonly object _lock = new object();
        private Dictionary<string, BookLending> _currentLendings = new Dictionary<string, BookLending>(); // Barcode -> Lending Record

        private BookLending() { }

        public static BookLending Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new BookLending();
                        }
                    }
                }
                return _instance;
            }
        }

        public bool LendBook(string barcode, string memberId)
        {
            if (_currentLendings.ContainsKey(barcode))
            {
                Console.WriteLine($"Error: Book item '{barcode}' is already loaned out.");
                return false;
            }

            var lending = new BookLending
            {
                CreationDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(LibraryConstants.MAX_LENDING_DAYS),
                BookItemBarcode = barcode,
                MemberId = memberId
            };
            _currentLendings.Add(barcode, lending);
            Console.WriteLine($"Book item '{barcode}' loaned to member '{memberId}'. Due date: {lending.DueDate:yyyy-MM-dd}.");
            return true;
        }

        public BookLending FetchLendingDetails(string barcode)
        {
            _currentLendings.TryGetValue(barcode, out var lending);
            return lending;
        }

        public bool ReturnBook(string barcode)
        {
            if (_currentLendings.TryGetValue(barcode, out var lending))
            {
                lending.ReturnDate = DateTime.Now;
                _currentLendings.Remove(barcode); // Remove from current lendings after return
                Console.WriteLine($"Book item '{barcode}' returned by member '{lending.MemberId}'.");
                return true;
            }
            Console.WriteLine($"Error: Book item '{barcode}' was not found in current lendings.");
            return false;
        }

        public bool RenewBook(string barcode, string memberId)
        {
            if (_currentLendings.TryGetValue(barcode, out var lending))
            {
                if (lending.MemberId != memberId)
                {
                    Console.WriteLine($"Error: Book item '{barcode}' is not currently loaned by member '{memberId}'.");
                    return false;
                }
                lending.DueDate = DateTime.Now.AddDays(LibraryConstants.MAX_LENDING_DAYS); // Extend due date
                Console.WriteLine($"Book item '{barcode}' renewed by member '{memberId}'. New due date: {lending.DueDate:yyyy-MM-dd}.");
                return true;
            }
            Console.WriteLine($"Error: Book item '{barcode}' not found in current lendings for renewal.");
            return false;
        }

        // For distributed systems: need to consider how these lists are synced/replicated
        // For now, this is an in-memory representation.
    }
}
