namespace AbstractFactory.V2;

/// <summary>
/// Concrete Factory: Creates all AWS cloud storage repositories.
/// Every repository from this factory uses AWS infrastructure (S3, DynamoDB, ElasticSearch).
/// Guaranteed consistency — no mixing of cloud and local repos.
/// </summary>
public class AwsStorageFactory : IStorageFactory
{
    public IFileRepository CreateFileRepository() => new S3FileRepository();
    public IMetadataRepository CreateMetadataRepository() => new DynamoDbMetadataRepository();
    public ISearchIndex CreateSearchIndex() => new ElasticSearchIndex();
}
