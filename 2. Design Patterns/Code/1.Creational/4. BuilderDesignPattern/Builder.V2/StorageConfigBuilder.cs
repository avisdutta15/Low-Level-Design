namespace Builder.V2;

/// <summary>
/// Fluent Builder for StorageConfig.
/// 
/// - Required parameters are set in the constructor (provider, bucketName)
/// - Optional parameters have sensible defaults
/// - Each setter returns 'this' for method chaining (fluent API)
/// - Build() validates the configuration and returns the immutable product
/// </summary>
public class StorageConfigBuilder
{
    // Required
    private readonly string _provider;
    private readonly string _bucketName;

    // Optional with defaults
    private string _region = "us-east-1";
    private int _maxRetries = 3;
    private int _timeoutSeconds = 30;
    private bool _enableEncryption = false;
    private string? _encryptionKey = null;
    private bool _enableVersioning = false;
    private bool _enableLogging = false;
    private string? _logPath = null;
    private long _maxFileSizeBytes = long.MaxValue;
    private string[] _allowedExtensions = Array.Empty<string>();

    /// <summary>
    /// Constructor takes only REQUIRED parameters.
    /// Everything else is optional with sensible defaults.
    /// </summary>
    public StorageConfigBuilder(string provider, string bucketName)
    {
        _provider = provider;
        _bucketName = bucketName;
    }

    public StorageConfigBuilder WithRegion(string region)
    {
        _region = region;
        return this; // Fluent: returns 'this' for chaining
    }

    public StorageConfigBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    public StorageConfigBuilder WithTimeout(int timeoutSeconds)
    {
        _timeoutSeconds = timeoutSeconds;
        return this;
    }

    public StorageConfigBuilder WithEncryption(string encryptionKey)
    {
        _enableEncryption = true;
        _encryptionKey = encryptionKey;
        return this;
    }

    public StorageConfigBuilder WithVersioning()
    {
        _enableVersioning = true;
        return this;
    }

    public StorageConfigBuilder WithLogging(string logPath)
    {
        _enableLogging = true;
        _logPath = logPath;
        return this;
    }

    public StorageConfigBuilder WithMaxFileSize(long maxFileSizeBytes)
    {
        _maxFileSizeBytes = maxFileSizeBytes;
        return this;
    }

    public StorageConfigBuilder WithAllowedExtensions(params string[] extensions)
    {
        _allowedExtensions = extensions;
        return this;
    }

    /// <summary>
    /// Validates the configuration and builds the immutable StorageConfig.
    /// Throws if the configuration is invalid.
    /// </summary>
    public StorageConfig Build()
    {
        // Validation — enforce business rules
        if (string.IsNullOrWhiteSpace(_provider))
            throw new InvalidOperationException("Provider is required.");

        if (string.IsNullOrWhiteSpace(_bucketName))
            throw new InvalidOperationException("Bucket name is required.");

        if (_enableEncryption && string.IsNullOrWhiteSpace(_encryptionKey))
            throw new InvalidOperationException(
                "Encryption key is required when encryption is enabled.");

        if (_enableLogging && string.IsNullOrWhiteSpace(_logPath))
            throw new InvalidOperationException(
                "Log path is required when logging is enabled.");

        if (_maxRetries < 0)
            throw new InvalidOperationException("Max retries cannot be negative.");

        if (_timeoutSeconds <= 0)
            throw new InvalidOperationException("Timeout must be positive.");

        return new StorageConfig(
            _provider,
            _bucketName,
            _region,
            _maxRetries,
            _timeoutSeconds,
            _enableEncryption,
            _encryptionKey,
            _enableVersioning,
            _enableLogging,
            _logPath,
            _maxFileSizeBytes,
            _allowedExtensions
        );
    }
}
