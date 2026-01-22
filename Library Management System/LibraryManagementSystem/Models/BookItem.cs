using LibraryManagementSystem.Enums;
using LibraryManagementSystem.Models.Abstract;
using LibraryManagementSystem.Utils;

namespace LibraryManagementSystem.Models
{
    public class BookItem
    {
        public required string Barcode { get; set; }
        public required LibraryMaterial Material { get; set; } // Can be Book, Magazine, DVD etc.
        public bool IsReferenceOnly { get; set; }
        public DateTime? BorrowedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public double Price { get; set; }
        public BookFormat Format { get; set; } // Renamed from BookFormat to MaterialFormat if handling different materials
        public BookStatus Status { get; set; }
        public DateTime DateOfPurchase { get; set; }
        public required Rack PlacedAt { get; set; }

        // This method should be called by Member or Librarian, not directly by BookItem
        public bool Checkout(string memberId)
        {
            if (IsReferenceOnly)
            {
                Console.WriteLine("Error: This item is Reference only and can't be issued.");
                return false;
            }
            if (Status != BookStatus.Available)
            {
                Console.WriteLine($"Error: This item is currently {Status}.");
                return false;
            }

            // Centralized lending logic
            if (!BookLending.Instance.LendBook(Barcode, memberId))
            {
                return false;
            }

            Status = BookStatus.Loaned;
            BorrowedDate = DateTime.Now;
            DueDate = DateTime.Now.AddDays(LibraryConstants.MAX_LENDING_DAYS);
            return true;
        }

        public void UpdateBookItemStatus(BookStatus newStatus)
        {
            Status = newStatus;
        }

        public void UpdateDueDate(DateTime newDueDate)
        {
            DueDate = newDueDate;
        }
    }
}
