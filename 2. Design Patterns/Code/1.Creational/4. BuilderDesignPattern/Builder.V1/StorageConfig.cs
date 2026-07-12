namespace Builder.V1;

/// <summary>
/// A complex object with many configuration options.
/// 
/// Problem: The constructor has too many parameters (telescoping constructor).
/// Some are required, some are optional — but the constructor forces you to
/// provide ALL of them or create dozens of overloads.
/// </summary>
public class StorageConfig
{
    public string Provider { get; }
    public string BucketName { get; }
    public string Region { get; }
    public int MaxRetries { get; }
    public int TimeoutSeconds { get; }
    public bool EnableEncryption { get; }
    public string? EncryptionKey { get; }
    public bool EnableVersioning { get; }
    public bool EnableLogging { get; }
    public string? LogPath { get; }
    public long MaxFileSizeBytes { get; }
    public string[] AllowedExtensions { get; }

    // Telescoping constructor — NIGHTMARE to use!
    public StorageConfig(
        string provider,
        string bucketName,
        string region,
        int maxRetries,
        int timeoutSeconds,
        bool enableEncryption,
        string? encryptionKey,
        bool enableVersioning,
        bool enableLogging,
        string? logPath,
        long maxFileSizeBytes,
        string[] allowedExtensions)
    {
        Provider = provider;
        BucketName = bucketName;
        Region = region;
        MaxRetries = maxRetries;
        TimeoutSeconds = timeoutSeconds;
        EnableEncryption = enableEncryption;
        EncryptionKey = encryptionKey;
        EnableVersioning = enableVersioning;
        EnableLogging = enableLogging;
        LogPath = logPath;
        MaxFileSizeBytes = maxFileSizeBytes;
        AllowedExtensions = allowedExtensions;
    }

    public void PrintConfig()
    {
        Console.WriteLine($"  Provider: {Provider}");
        Console.WriteLine($"  Bucket: {BucketName}");
        Console.WriteLine($"  Region: {Region}");
        Console.WriteLine($"  Max Retries: {MaxRetries}");
        Console.WriteLine($"  Timeout: {TimeoutSeconds}s");
        Console.WriteLine($"  Encryption: {EnableEncryption} (Key: {EncryptionKey ?? "none"})");
        Console.WriteLine($"  Versioning: {EnableVersioning}");
        Console.WriteLine($"  Logging: {EnableLogging} (Path: {LogPath ?? "none"})");
        Console.WriteLine($"  Max File Size: {MaxFileSizeBytes} bytes");
        Console.WriteLine($"  Allowed Extensions: [{string.Join(", ", AllowedExtensions)}]");
    }
}
