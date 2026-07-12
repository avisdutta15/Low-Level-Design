namespace State.V2;

public class UploadingState : IUploadState
{
    public string Name => "Uploading";

    public void Validate(FileUploadJob job)
    {
        Console.WriteLine($"  [Uploading] Cannot validate while uploading");
    }

    public void Upload(FileUploadJob job)
    {
        Console.WriteLine($"  [Uploading] Already uploading");
    }

    public void Cancel(FileUploadJob job)
    {
        Console.WriteLine($"  [Uploading] Cannot cancel mid-upload");
    }

    public void Retry(FileUploadJob job)
    {
        Console.WriteLine($"  [Uploading] Cannot retry while uploading");
    }
}
