namespace State.V2;

public class CancelledState : IUploadState
{
    public string Name => "Cancelled";

    public void Validate(FileUploadJob job)
    {
        Console.WriteLine($"  [Cancelled] Job was cancelled");
    }

    public void Upload(FileUploadJob job)
    {
        Console.WriteLine($"  [Cancelled] Job was cancelled");
    }

    public void Cancel(FileUploadJob job)
    {
        Console.WriteLine($"  [Cancelled] Already cancelled");
    }

    public void Retry(FileUploadJob job)
    {
        Console.WriteLine($"  [Cancelled] Cannot retry cancelled job");
    }
}
