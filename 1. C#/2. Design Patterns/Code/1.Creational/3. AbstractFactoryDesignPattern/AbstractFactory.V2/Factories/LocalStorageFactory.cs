namespace AbstractFactory.V2;

/// <summary>
/// Concrete Factory: Creates all local/development storage repositories.
/// Every repository from this factory uses local infrastructure (disk, SQLite, in-memory).
/// Guaranteed consistency — no mixing of local and cloud repos.
/// </summary>
public class LocalStorageFactory : IStorageFactory
{
    public IFileRepository CreateFileRepository() => new LocalFileRepository();
    public IMetadataRepository CreateMetadataRepository() => new SqliteMetadataRepository();
    public ISearchIndex CreateSearchIndex() => new InMemorySearchIndex();
}
