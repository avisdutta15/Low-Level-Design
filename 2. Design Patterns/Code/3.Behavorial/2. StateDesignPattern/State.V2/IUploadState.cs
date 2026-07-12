namespace State.V2;

/// <summary>
/// State interface — defines all actions that behavior depends on state.
/// Each concrete state implements these differently.
/// </summary>
public interface IUploadState
{
    string Name { get; }
    void Validate(FileUploadJob job);
    void Upload(FileUploadJob job);
    void Cancel(FileUploadJob job);
    void Retry(FileUploadJob job);
}
