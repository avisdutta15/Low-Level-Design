namespace LibraryManagementSystem.Models
{
    public class LibraryCard
    {
        public required string CardId { get; set; }
        public required string MemberId { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public bool IsActive => DateTime.Now <= ExpirationDate;
    }
}