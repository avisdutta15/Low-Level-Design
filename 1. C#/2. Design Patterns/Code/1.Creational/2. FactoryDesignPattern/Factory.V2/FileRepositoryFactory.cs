namespace Factory.V2;

/// <summary>
/// The Factory — encapsulates object creation logic.
/// 
/// The client only knows about IFileRepository and StorageType.
/// It never references S3FileRepository, LocalFileRepository, etc. directly.
/// 
/// To add a new storage provider:
///   1. Create the new class implementing IFileRepository
///   2. Add a new enum value
///   3. Add one case to this factory
///   4. Client code is UNCHANGED — Open/Closed Principle satisfied.
/// </summary>
public enum StorageType
{
    S3,
    Local,
    AzureBlob
}

public class FileRepositoryFactory
{
    public IFileRepository CreateRepository(StorageType type)
    {
        return type switch
        {
            StorageType.S3 => new S3FileRepository(),
            StorageType.Local => new LocalFileRepository(),
            StorageType.AzureBlob => new AzureBlobFileRepository(),
            _ => throw new ArgumentException($"Unknown storage type: {type}")
        };
    }
}
