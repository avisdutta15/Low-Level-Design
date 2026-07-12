namespace State.V2;

/// <summary>
/// Context — holds pre-created state instances and delegates all actions.
/// States are created ONCE and reused — no allocations on transitions.
/// </summary>
public class FileUploadJob
{
    public string FileName { get; }
    public byte[] Content { get; }
    public IUploadState CurrentState { get; private set; }
    public string? ErrorMessage { get; set; }

    // Pre-created state instances — reused across transitions
    public PendingState PendingState { get; }
    public ValidatedState ValidatedState { get; }
    public UploadingState UploadingState { get; }
    public CompletedState CompletedState { get; }
    public FailedState FailedState { get; }
    public CancelledState CancelledState { get; }

    public FileUploadJob(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;

        // Create all states ONCE
        PendingState = new PendingState();
        ValidatedState = new ValidatedState();
        UploadingState = new UploadingState();
        CompletedState = new CompletedState();
        FailedState = new FailedState();
        CancelledState = new CancelledState();

        // Initial state
        CurrentState = PendingState;
    }

    public void TransitionTo(IUploadState newState)
    {
        Console.WriteLine($"  [Transition] {CurrentState.Name} → {newState.Name}");
        CurrentState = newState;
    }

    // All actions simply delegate to the current state
    public void Validate() => CurrentState.Validate(this);
    public void Upload() => CurrentState.Upload(this);
    public void Cancel() => CurrentState.Cancel(this);
    public void Retry() => CurrentState.Retry(this);
}
