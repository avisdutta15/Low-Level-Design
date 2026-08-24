namespace Decorator.V2;

/// <summary>
/// Decorator: Adds retry logic around ANY IFileRepository.
/// Retries failed operations up to N times before throwing.
/// </summary>
public class RetryDecorator : IFileRepository
{
    private readonly IFileRepository _inner;
    private readonly int _maxRetries;

    public RetryDecorator(IFileRepository inner, int maxRetries = 3)
    {
        _inner = inner;
        _maxRetries = maxRetries;
    }

    public void Upload(string fileName, byte[] content)
    {
        ExecuteWithRetry(() => _inner.Upload(fileName, content), "Upload", fileName);
    }

    public byte[] Download(string fileName)
    {
        byte[] result = Array.Empty<byte>();
        ExecuteWithRetry(() => { result = _inner.Download(fileName); }, "Download", fileName);
        return result;
    }

    public void Delete(string fileName)
    {
        ExecuteWithRetry(() => _inner.Delete(fileName), "Delete", fileName);
    }

    private void ExecuteWithRetry(Action action, string operation, string fileName)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                action();
                if (attempt > 1)
                    Console.WriteLine($"  [RETRY] {operation} '{fileName}' succeeded on attempt {attempt}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [RETRY] {operation} '{fileName}' failed (attempt {attempt}/{_maxRetries}): {ex.Message}");
                if (attempt == _maxRetries)
                    throw;
            }
        }
    }
}
