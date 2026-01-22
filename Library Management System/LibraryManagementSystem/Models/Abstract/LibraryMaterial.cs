namespace LibraryManagementSystem.Models.Abstract
{
    public abstract class LibraryMaterial
    {
        public required string Id { get; set; }     // Can be used for all materials as a unique identifier
        public required string Title { get; set; }  // Title of the material
    }
}
