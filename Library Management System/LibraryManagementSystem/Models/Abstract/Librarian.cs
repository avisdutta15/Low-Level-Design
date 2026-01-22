using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Models.Abstract
{
    public class Librarian : Account
    {
        public override void ShowError(string message)
        {
            Console.WriteLine($"Error: {message}");
        }

        // Method to add a new book item to the library
        public bool AddBookItem(BookItem bookItem)
        {
            // In a real system, this would interact with a database or persistent storage
            // For this example, we'll add it to the Catalog (acting as a repository)
            Catalog.Instance.AddBookItem(bookItem);
            Console.WriteLine($"Librarian '{Person.Name}' added book item '{bookItem.Material.Title}' (Barcode: {bookItem.Barcode}).");
            return true;
        }

        public bool BlockMember(Member member)
        {
            if (member == null)
            {
                ShowError("Member not found.");
                return false;
            }
            member.Status = AccountStatus.Blacklisted;
            Console.WriteLine($"Librarian '{Person.Name}' blocked member '{member.Person.Name}' (ID: {member.Id}).");
            return true;
        }

        public bool UnblockMember(Member member)
        {
            if (member == null)
            {
                ShowError("Member not found.");
                return false;
            }
            member.Status = AccountStatus.Active;
            Console.WriteLine($"Librarian '{Person.Name}' unblocked member '{member.Person.Name}' (ID: {member.Id}).");
            return true;
        }

        public LibraryCard IssueLibraryCard(Member member)
        {
            if (member == null)
            {
                ShowError("Cannot issue card: Member is null.");
                return null;
            }

            var card = new LibraryCard
            {
                CardId = Guid.NewGuid().ToString(), // Unique ID for the card
                MemberId = member.Id,
                IssueDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddYears(1) // Example: Card valid for 1 year
            };
            Console.WriteLine($"Librarian '{Person.Name}' issued library card '{card.CardId}' to member '{member.Person.Name}'.");
            return card;
        }

        // Method to remove a book item from the library
        public void RemoveBookItem(string barcode)
        {
            // Logic to remove book item from the library database
        }
        // Method to update book item details
        public void UpdateBookItem(BookItem bookItem)
        {
            // Logic to update book item in the library database
        }
        // Method to view all book items
        public List<BookItem> ViewAllBookItems()
        {
            // Logic to retrieve all book items from the library database
            return new List<BookItem>();
        }

        
    }
}
