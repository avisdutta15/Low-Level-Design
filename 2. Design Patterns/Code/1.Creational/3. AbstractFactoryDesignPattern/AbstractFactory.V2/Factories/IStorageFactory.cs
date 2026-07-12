namespace AbstractFactory.V2;

/// <summary>
/// Abstract Factory interface — defines the contract for creating
/// a FAMILY of related storage repositories.
/// 
/// Each concrete factory creates repositories that are consistent
/// with each other (same environment/infrastructure).
/// </summary>
public interface IStorageFactory
{
    IFileRepository CreateFileRepository();
    IMetadataRepository CreateMetadataRepository();
    ISearchIndex CreateSearchIndex();
}
