namespace AbstractFactory.V1;

public interface IMetadataRepository
{
    void Save(string key, Dictionary<string, string> metadata);
    Dictionary<string, string>? Get(string key);
    void Delete(string key);
}
