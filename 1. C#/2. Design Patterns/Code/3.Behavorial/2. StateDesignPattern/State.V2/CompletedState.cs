namespace State.V2;

public class CompletedState : IUploadState
{
    public string Name => "Completed";

    public void Validate(FileUploadJob job)
    {
        Console.WriteLine($"  [Completed] Upload already complete");
    }

    public void Upload(FileUploadJob job)
    {
        Console.WriteLine($"  [Completed] Upload already complete");
    }

    public void Cancel(FileUploadJob job)
    {
        Console.WriteLine($"  [Completed] Cannot cancel completed upload");
    }

    public void Retry(FileUploadJob job)
    {
        Console.WriteLine($"  [Completed] Nothing to retry — upload succeeded");
    }
}
