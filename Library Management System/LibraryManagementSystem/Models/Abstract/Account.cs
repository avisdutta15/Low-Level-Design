using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Models.Abstract
{
    public abstract class Account
    {
        // Unique identifier for the account : Used by internal services to manage accounts
        public required Guid Id { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required AccountStatus Status { get; set; }
        public required Person Person { get; set; }

        // For displaying error messages
        public abstract void ShowError(string message); 
    }
}
