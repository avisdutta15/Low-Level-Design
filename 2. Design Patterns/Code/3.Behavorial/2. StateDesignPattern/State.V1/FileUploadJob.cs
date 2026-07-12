namespace State.V1;

/// <summary>
/// Without State Pattern: All state transitions managed via if/else on a string/enum field.
/// 
/// Problems:
///   - Massive switch/if-else blocks in every method
///   - Adding a new state = modifying EVERY method
///   - Invalid transitions aren't caught cleanly
///   - State-specific logic is scattered across methods
///   - Violates OCP: can't add states without modifying existing code
/// </summary>
public class FileUploadJob
{
    public string FileName { get; }
    public byte[] Content { get; }
    public string CurrentState { get; private set; } = "Pending";
    public string? ErrorMessage { get; private set; }

    public FileUploadJob(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;
    }

    public void Validate()
    {
        if (CurrentState == "Pending")
        {
            Console.WriteLine($"  [{CurrentState}] Validating '{FileName}'...");
            // simulate validation
            if (Content.Length > 10 * 1024 * 1024)
            {
                CurrentState = "Failed";
                ErrorMessage = "File too large";
                Console.WriteLine($"  [Failed] Validation failed: {ErrorMessage}");
                return;
            }
            CurrentState = "Validated";
            Console.WriteLine($"  [Validated] '{FileName}' passed validation");
        }
        else if (CurrentState == "Validated")
        {
            Console.WriteLine($"  [Error] Already validated");
        }
        else if (CurrentState == "Uploading")
        {
            Console.WriteLine($"  [Error] Can't validate while uploading");
        }
        else if (CurrentState == "Completed")
        {
            Console.WriteLine($"  [Error] Upload already completed");
        }
        else if (CurrentState == "Failed")
        {
            Console.WriteLine($"  [Error] Job has failed, cannot validate");
        }
    }

    public void Upload()
    {
        if (CurrentState == "Validated")
        {
            CurrentState = "Uploading";
            Console.WriteLine($"  [Uploading] Uploading '{FileName}' to storage...");
            // simulate upload
            CurrentState = "Completed";
            Console.WriteLine($"  [Completed] '{FileName}' uploaded successfully");
        }
        else if (CurrentState == "Pending")
        {
            Console.WriteLine($"  [Error] Must validate before uploading");
        }
        else if (CurrentState == "Uploading")
        {
            Console.WriteLine($"  [Error] Already uploading");
        }
        else if (CurrentState == "Completed")
        {
            Console.WriteLine($"  [Error] Already completed");
        }
        else if (CurrentState == "Failed")
        {
            Console.WriteLine($"  [Error] Job has failed, cannot upload");
        }
    }

    public void Cancel()
    {
        if (CurrentState == "Pending" || CurrentState == "Validated")
        {
            CurrentState = "Cancelled";
            Console.WriteLine($"  [Cancelled] Job cancelled");
        }
        else if (CurrentState == "Uploading")
        {
            Console.WriteLine($"  [Error] Can't cancel while uploading");
        }
        else if (CurrentState == "Completed")
        {
            Console.WriteLine($"  [Error] Can't cancel completed upload");
        }
        else if (CurrentState == "Failed" || CurrentState == "Cancelled")
        {
            Console.WriteLine($"  [Error] Already in terminal state: {CurrentState}");
        }
    }

    public void Retry()
    {
        if (CurrentState == "Failed")
        {
            CurrentState = "Pending";
            ErrorMessage = null;
            Console.WriteLine($"  [Pending] Retrying — reset to Pending");
        }
        else
        {
            Console.WriteLine($"  [Error] Can only retry from Failed state (current: {CurrentState})");
        }
    }
}
