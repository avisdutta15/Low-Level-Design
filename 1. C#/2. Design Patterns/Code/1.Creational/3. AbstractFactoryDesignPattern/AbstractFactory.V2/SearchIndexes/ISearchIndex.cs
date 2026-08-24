namespace AbstractFactory.V2;

public interface ISearchIndex
{
    void Index(string documentId, string content);
    List<string> Search(string query);
}
