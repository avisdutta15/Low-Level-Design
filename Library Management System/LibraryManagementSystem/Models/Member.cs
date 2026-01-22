using LibraryManagementSystem.Enums;
using LibraryManagementSystem.Models.Abstract;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Utils;
using Microsoft.VisualBasic;

namespace LibraryManagementSystem.Models
{
    public class Member : Account
    {
        private int _totalBooksCheckedOut; // Backing field for thread-safe operations

        public int TotalBooksCheckedOut
        {
            get => _totalBooksCheckedOut;
            private set => _totalBooksCheckedOut = value;
        }

        public List<BookLending> BorrowingHistory { get; set; } = new List<BookLending>();
        public required LibraryCard LibraryCard { get; set; }

        public override void ShowError(string message)
        {
            Console.WriteLine($"Error: {message}");
        }

        public bool CheckoutBookItem(BookItem bookItem)
        {
            if (bookItem == null)
            {
                ShowError("Book item not found.");
                return false;
            }

            if (Status != AccountStatus.Active)
            {
                ShowError($"Your account is {Status}. You cannot check out books.");
                return false;
            }

            if (TotalBooksCheckedOut >= LibraryConstants.MAX_BOOKS_ISSUED_TO_A_USER)
            {
                ShowError($"You have reached the maximum limit of {LibraryConstants.MAX_BOOKS_ISSUED_TO_A_USER} books checked out.");
                return false;
            }

            BookReservation reservation = BookReservation.Instance.FetchReservationDetails(bookItem.Barcode);

            if (reservation != null && reservation.MemberId != this.Id)
            {
                // book item has a pending reservation from another user
                ShowError("This book is reserved by another member.");
                return false;
            }
            else if (reservation != null)
            {
                // book item has a pending reservation from the given member, update it
                reservation.UpdateStatus(ReservationStatus.Completed);
                Console.WriteLine($"Your reservation for '{bookItem.Material.Title}' has been completed.");
            }

            if (!bookItem.Checkout(this.Id))
            {
                ShowError("Failed to checkout the book item.");
                return false;
            }

            IncrementTotalBooksCheckedout();
            BorrowingHistory.Add(BookLending.Instance.FetchLendingDetails(bookItem.Barcode)); // Add to history
            //NotificationService.Instance.SendEmailNotification(this.Person.Email, "Book Checked Out", $"You have successfully checked out '{bookItem.Material.Title}'. Due date: {bookItem.DueDate:yyyy-MM-dd}.");
            return true;
        }

        private void CheckForFine(string bookItemBarcode)
        {
            BookLending bookLending = BookLending.Instance.FetchLendingDetails(bookItemBarcode);
            if (bookLending == null)
            {
                Console.WriteLine("No lending details found for this book item.");
                return;
            }

            DateTime dueDate = bookLending.DueDate;
            DateTime today = DateTime.Now;

            // check if the book has been returned within the due date
            if (today.CompareTo(dueDate) > 0)
            {
                TimeSpan diff = today - dueDate;
                long diffDays = (long)Math.Ceiling(diff.TotalDays); // Ensure positive days

                if (diffDays > 0) // Only collect fine if overdue
                {
                    FineService.Instance.CollectFine(this.Id, diffDays);
                }
            }
        }

        public void ReturnBookItem(BookItem bookItem)
        {
            if (bookItem == null)
            {
                ShowError("Book item not found.");
                return;
            }

            if (bookItem.Status != BookStatus.Loaned)
            {
                ShowError($"This book is not currently loaned. Current status: {bookItem.Status}");
                return;
            }

            CheckForFine(bookItem.Barcode);

            BookLending.Instance.ReturnBook(bookItem.Barcode); // Update lending record

            BookReservation bookReservation = BookReservation.Instance.FetchReservationDetails(bookItem.Barcode);
            if (bookReservation != null)
            {
                // book item has a pending reservation
                bookItem.UpdateBookItemStatus(BookStatus.Reserved);
                bookReservation.SendBookAvailableNotification();
            }
            else
            {
                bookItem.UpdateBookItemStatus(BookStatus.Available);
            }

            DecrementTotalBooksCheckedout();
            //NotificationService.Instance.SendSmsNotification(this.Person.Phone, $"You have successfully returned '{bookItem.Material.Title}'.");
            Console.WriteLine($"Member '{Person.Name}' returned '{bookItem.Material.Title}' (Barcode: {bookItem.Barcode}).");

        }

        public bool RenewBookItem(BookItem bookItem)
        {
            if (bookItem == null)
            {
                ShowError("Book item not found.");
                return false;
            }

            if (bookItem.Status != BookStatus.Loaned)
            {
                ShowError($"This book is not currently loaned. Current status: {bookItem.Status}");
                return false;
            }

            CheckForFine(bookItem.Barcode);

            BookReservation bookReservation = BookReservation.Instance.FetchReservationDetails(bookItem.Barcode);
            // check if this book item has a pending reservation from another member
            if (bookReservation != null && bookReservation.MemberId != this.Id)
            {
                ShowError("This book is reserved by another member and cannot be renewed.");
                // member.DecrementTotalBooksCheckedout(); // This seems incorrect for renewal, as they still have it
                // bookItem.UpdateBookItemState(BookStatus.RESERVED); // This also seems incorrect for renewal
                bookReservation.SendBookAvailableNotification(); // Notify the reserving member
                return false;
            }
            else if (bookReservation != null)
            {
                // book item has a pending reservation from this member, update it
                bookReservation.UpdateStatus(ReservationStatus.Completed);
                Console.WriteLine($"Your reservation for '{bookItem.Material.Title}' has been completed upon renewal.");
            }

            BookLending.Instance.RenewBook(bookItem.Barcode, this.Id); // Update lending record for renewal
            bookItem.UpdateDueDate(DateTime.Now.AddDays(LibraryConstants.MAX_LENDING_DAYS));
            //NotificationService.Instance.SendEmailNotification(this.Person.Email, "Book Renewed", $"You have successfully renewed '{bookItem.Material.Title}'. New due date: {bookItem.DueDate:yyyy-MM-dd}.");
            Console.WriteLine($"Member '{Person.Name}' renewed '{bookItem.Material.Title}' (Barcode: {bookItem.Barcode}). New due date: {bookItem.DueDate:yyyy-MM-dd}.");
            return true;
        }

        public void IncrementTotalBooksCheckedout()
        {
            Interlocked.Increment(ref _totalBooksCheckedOut); // Thread-safe increment
        }

        public void DecrementTotalBooksCheckedout()
        {
            Interlocked.Decrement(ref _totalBooksCheckedOut); // Thread-safe decrement
        }
    }
}