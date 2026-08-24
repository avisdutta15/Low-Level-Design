namespace ChainOfResponsibility.V2;

/// <summary>
/// Base handler — implements the chain linking logic.
/// Concrete handlers override Handle() and call base.Handle()
/// to pass to the next handler in the chain.
/// </summary>
public abstract class BaseUploadHandler : IUploadHandler
{
    private IUploadHandler? _next;

    public IUploadHandler SetNext(IUploadHandler next)
    {
        _next = next;
        return next; // enables fluent chaining: a.SetNext(b).SetNext(c)
    }

    public virtual bool Handle(UploadRequest request)
    {
        // If there's a next handler, pass the request along
        if (_next != null)
            return _next.Handle(request);

        // End of chain — all handlers passed, request is approved
        return true;
    }
}
