namespace LibraryManagementSystem.Models
{
    public class Address
    {
        public required string City { get; set; }
        public required string State { get; set; } 
        public required string ZipCode { get; set; } 
        public required string Country { get; set; } 
        public string? StreetAddress { get; set; }
    }
}
