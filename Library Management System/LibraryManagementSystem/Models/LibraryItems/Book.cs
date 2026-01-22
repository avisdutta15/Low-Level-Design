using LibraryManagementSystem.Models.Abstract;

namespace LibraryManagementSystem.Models.LibraryItems
{
    public class Book : LibraryMaterial
    {
        public required string ISBN;
        public required string Subject;
        public required string Publisher;
        public string? Language;
        public int NumberOfPages;
        public required List<Author> Authors;
    }
}
