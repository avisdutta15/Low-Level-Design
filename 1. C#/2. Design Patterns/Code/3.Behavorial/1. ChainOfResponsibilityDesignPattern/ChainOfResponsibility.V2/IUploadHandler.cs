namespace ChainOfResponsibility.V2;

/// <summary>
/// The Handler interface — each handler in the chain implements this.
/// Each handler either processes the request or passes it to the next handler.
/// </summary>
public interface IUploadHandler
{
    IUploadHandler SetNext(IUploadHandler next);
    bool Handle(UploadRequest request);
}
