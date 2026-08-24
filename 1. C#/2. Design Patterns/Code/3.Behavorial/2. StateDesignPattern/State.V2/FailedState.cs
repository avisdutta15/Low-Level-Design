namespace State.V2;

public class FailedState : IUploadState
{
    public string Name => "Failed";

    public void Validate(FileUploadJob job)
    {
        Console.WriteLine($"  [Failed] Cannot validate — job failed: {job.ErrorMessage}");
    }

    public void Upload(FileUploadJob job)
    {
        Console.WriteLine($"  [Failed] Cannot upload — job failed: {job.ErrorMessage}");
    }

    public void Cancel(FileUploadJob job)
    {
        Console.WriteLine($"  [Failed] Already in terminal state");
    }

    public void Retry(FileUploadJob job)
    {
        Console.WriteLine($"  [Failed] Retrying — resetting to Pending");
        job.ErrorMessage = null;
        job.TransitionTo(job.PendingState);
    }
}
