namespace State.V2;

public class PendingState : IUploadState
{
    public string Name => "Pending";

    public void Validate(FileUploadJob job)
    {
        Console.WriteLine($"  [Pending] Validating '{job.FileName}'...");

        if (job.Content.Length > 10 * 1024 * 1024)
        {
            job.ErrorMessage = "File too large";
            job.TransitionTo(job.FailedState);
            return;
        }

        job.TransitionTo(job.ValidatedState);
    }

    public void Upload(FileUploadJob job)
    {
        Console.WriteLine($"  [Pending] Cannot upload — must validate first");
    }

    public void Cancel(FileUploadJob job)
    {
        job.TransitionTo(job.CancelledState);
    }

    public void Retry(FileUploadJob job)
    {
        Console.WriteLine($"  [Pending] Nothing to retry — already pending");
    }
}
