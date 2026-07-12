namespace AbstractFactory.V1;

public interface ISearchIndex
{
    void Index(string documentId, string content);
    List<string> Search(string query);
}
