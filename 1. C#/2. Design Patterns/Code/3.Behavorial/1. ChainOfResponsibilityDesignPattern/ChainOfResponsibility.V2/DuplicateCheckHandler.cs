namespace ChainOfResponsibility.V2;

/// <summary>
/// Handler 5: Checks if a file with the same name already exists.
/// </summary>
public class DuplicateCheckHandler : BaseUploadHandler
{
    private readonly HashSet<string> _existingFiles;

    public DuplicateCheckHandler(IEnumerable<string>? existingFiles = null)
    {
        _existingFiles = new HashSet<string>(existingFiles ?? Enumerable.Empty<string>());
    }

    public override bool Handle(UploadRequest request)
    {
        if (_existingFiles.Contains(request.FileName))
        {
            Console.WriteLine($"  [Duplicate] REJECTED: '{request.FileName}' already exists");
            return false;
        }

        Console.WriteLine($"  [Duplicate] PASSED: No duplicate found");
        return base.Handle(request);
    }
}
