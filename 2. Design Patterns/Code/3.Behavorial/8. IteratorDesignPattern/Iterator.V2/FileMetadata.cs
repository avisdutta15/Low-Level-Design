namespace Iterator.V2;

public class FileMetadata
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string Author { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
