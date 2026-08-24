namespace Mediator.V2;

/// <summary>
/// Component: Quota — tracks storage usage.
/// Does NOT know about Storage or Notification.
/// </summary>
public class QuotaComponent : BaseComponent
{
    private long _usedBytes;
    private readonly long _maxBytes;

    public QuotaComponent(IStorageMediator mediator, long maxBytes = 100 * 1024 * 1024)
        : base(mediator)
    {
        _maxBytes = maxBytes;
    }

    public bool HasSpace(long bytes) => (_usedBytes + bytes) <= _maxBytes;

    public void ConsumeSpace(long bytes)
    {
        _usedBytes += bytes;
        Console.WriteLine($"  [Quota] Used: {_usedBytes}/{_maxBytes} bytes");

        if (_usedBytes > _maxBytes * 0.9)
        {
            Mediator.Notify(this, "QuotaWarning", new Dictionary<string, object>
            {
                ["usedBytes"] = _usedBytes,
                ["maxBytes"] = _maxBytes
            });
        }

        if (_usedBytes >= _maxBytes)
        {
            Mediator.Notify(this, "QuotaExceeded");
        }
    }

    public void ReleaseSpace(long bytes)
    {
        bool wasExceeded = _usedBytes >= _maxBytes;
        _usedBytes -= bytes;
        Console.WriteLine($"  [Quota] Released {bytes} bytes. Used: {_usedBytes}/{_maxBytes}");

        if (wasExceeded && _usedBytes < _maxBytes)
        {
            Mediator.Notify(this, "QuotaAvailable");
        }
    }
}
