namespace Mediator.V2;

/// <summary>
/// Component: Search Index — indexes/removes files.
/// Does NOT know about Storage or Quota.
/// </summary>
public class SearchIndexComponent : BaseComponent
{
    public SearchIndexComponent(IStorageMediator mediator) : base(mediator) { }

    public void IndexFile(string fileName, string author)
        => Console.WriteLine($"  [Search] Indexed '{fileName}' by {author}");

    public void RemoveFile(string fileName)
        => Console.WriteLine($"  [Search] Removed '{fileName}' from index");
}
