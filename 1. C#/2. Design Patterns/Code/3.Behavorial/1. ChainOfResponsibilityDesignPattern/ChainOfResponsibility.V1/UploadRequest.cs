namespace ChainOfResponsibility.V1;

public class UploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string Author { get; set; } = string.Empty;
    public string UserRole { get; set; } = "reader";
    public long MaxAllowedSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
    public string[] AllowedExtensions { get; set; } = { ".pdf", ".docx", ".xlsx", ".txt" };
}
