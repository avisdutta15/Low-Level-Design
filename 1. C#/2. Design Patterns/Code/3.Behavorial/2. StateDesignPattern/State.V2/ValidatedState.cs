namespace State.V2;

public class ValidatedState : IUploadState
{
    public string Name => "Validated";

    public void Validate(FileUploadJob job)
    {
        Console.WriteLine($"  [Validated] Already validated");
    }

    public void Upload(FileUploadJob job)
    {
        job.TransitionTo(job.UploadingState);
        Console.WriteLine($"  [Uploading] Uploading '{job.FileName}' to storage...");
        job.TransitionTo(job.CompletedState);
    }

    public void Cancel(FileUploadJob job)
    {
        job.TransitionTo(job.CancelledState);
    }

    public void Retry(FileUploadJob job)
    {
        Console.WriteLine($"  [Validated] Nothing to retry — validation passed");
    }
}
