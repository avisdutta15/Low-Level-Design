# Iterator Design Pattern

## Table of Contents

- [What is the Iterator Pattern?](#what-is-the-iterator-pattern)
- [UML Diagram](#uml-diagram)
- [V1 — Why Do We Need Iterator?](#v1--why-do-we-need-iterator)
- [V2 — How to Implement Iterator](#v2--how-to-implement-iterator)
- [When to Use Iterator](#when-to-use-iterator)
- [LLD Problems Where Iterator Applies](#lld-problems-where-iterator-applies)

---

## What is the Iterator Pattern?

The Iterator pattern is a **behavioral design pattern** that provides a way to access elements of a collection sequentially without exposing the underlying data structure. The client uses a uniform `HasNext()`/`Next()` interface regardless of whether the collection is backed by an array, list, tree, or database cursor.

**Core Idea:**
- The **Collection** (Aggregate) creates an **Iterator**
- The Iterator provides `HasNext()` and `Next()` — the only API the client needs
- The internal data structure is completely hidden
- Multiple iterators can traverse the same collection independently
- Filtered/decorated iterators compose on top of base iterators

**Key Insight:** Separate the "how to traverse" from "what to traverse." The collection knows its structure; the iterator knows how to walk through it; the client knows neither.

---

## UML Diagram

```
┌──────────────────────────────────────┐
│    «interface» IFileCollection       │
│          (Aggregate)                 │
├──────────────────────────────────────┤
│ + CreateIterator(): IFileIterator    │
│ + CreateFilteredIterator(predicate)  │
│ + Add(file: FileMetadata)            │
│ + Count: int                         │
└──────────────────┬───────────────────┘
                   │ implements
         ┌─────────┴─────────┐
         │                   │
         ▼                   ▼
┌────────────────┐  ┌─────────────────┐
│ArrayFileCollect│  │ListFileCollection│
│  (array-backed)│  │  (list-backed)   │
├────────────────┤  ├─────────────────┤
│-_files: T[]    │  │-_files: List<T> │
│+CreateIterator │  │+CreateIterator  │
│ → ArrayIterator│  │ → ListIterator  │
└────────┬───────┘  └────────┬────────┘
         │ creates            │ creates
         ▼                    ▼
┌──────────────────────────────────────┐
│     «interface» IFileIterator        │
├──────────────────────────────────────┤
│ + HasNext(): bool                    │
│ + Next(): FileMetadata               │
│ + Reset()                            │
└──────────────────┬───────────────────┘
                   │ implements
     ┌─────────────┼──────────────┐
     │             │              │
     ▼             ▼              ▼
┌──────────┐ ┌──────────┐ ┌────────────────┐
│  Array   │ │  List    │ │  Filtered      │
│ Iterator │ │ Iterator │ │  Iterator      │
├──────────┤ ├──────────┤ ├────────────────┤
│-_index   │ │-_index   │ │-_inner: IFile  │
│-_array[] │ │-_list    │ │ Iterator       │
│          │ │          │ │-_predicate     │
│HasNext() │ │HasNext() │ │HasNext(): skips│
│Next()    │ │Next()    │ │ non-matching   │
└──────────┘ └──────────┘ └────────────────┘

Client code (works with ANY collection):
  IFileIterator iter = collection.CreateIterator();
  while (iter.HasNext())
      Process(iter.Next());
```

---

## V1 — Why Do We Need Iterator?

**Scenario:** File collections backed by different data structures (array, list, linked list). Each requires different traversal code.

**Without Iterator — traversal code depends on internal structure:**

```csharp
// Array: must use index
for (int i = 0; i < arrayCollection.Count; i++)
    Process(arrayCollection.GetAt(i));

// List: uses List<T> API
foreach (var file in listCollection.GetAll())
    Process(file);

// LinkedList: must follow Node.Next
var node = linkedCollection.Head;
while (node != null) { Process(node.Data); node = node.Next; }
```

**Problems:**

| Problem | Explanation |
|---------|-------------|
| Client knows structure | Array uses index, List uses foreach, LinkedList uses Node.Next |
| Change breaks all | Switching from array to tree = rewrite every loop |
| No uniform traversal | Can't write ONE method that works with all collections |
| Exposes internals | `GetAll()` returns the actual list — client can mutate it |
| No independent cursors | Two loops can't independently traverse the same collection |
| No filtered traversal | Filtering logic must be repeated in every loop |

---

## V2 — How to Implement Iterator

**Step 1: Iterator interface**

```csharp
public interface IFileIterator
{
    bool HasNext();
    FileMetadata Next();
    void Reset();
}
```

**Step 2: Collection interface (Aggregate)**

```csharp
public interface IFileCollection
{
    IFileIterator CreateIterator();
    IFileIterator CreateFilteredIterator(Func<FileMetadata, bool> predicate);
    void Add(FileMetadata file);
    int Count { get; }
}
```

**Step 3: Concrete collection + internal iterator**

```csharp
public class ArrayFileCollection : IFileCollection
{
    private readonly FileMetadata[] _files;
    private int _count;

    public ArrayFileCollection(int capacity) => _files = new FileMetadata[capacity];
    public void Add(FileMetadata file) => _files[_count++] = file;
    public int Count => _count;

    public IFileIterator CreateIterator() => new ArrayIterator(_files, _count);

    public IFileIterator CreateFilteredIterator(Func<FileMetadata, bool> predicate)
        => new FilteredFileIterator(CreateIterator(), predicate);

    // Iterator is PRIVATE — client only sees IFileIterator
    private class ArrayIterator : IFileIterator
    {
        private readonly FileMetadata[] _files;
        private readonly int _count;
        private int _pos;

        public ArrayIterator(FileMetadata[] files, int count)
        { _files = files; _count = count; }

        public bool HasNext() => _pos < _count;
        public FileMetadata Next() => _files[_pos++];
        public void Reset() => _pos = 0;
    }
}
```

**Step 4: Filtered iterator (decorator on any iterator)**

```csharp
public class FilteredFileIterator : IFileIterator
{
    private readonly IFileIterator _inner;
    private readonly Func<FileMetadata, bool> _predicate;
    private FileMetadata? _nextItem;

    public FilteredFileIterator(IFileIterator inner, Func<FileMetadata, bool> predicate)
    {
        _inner = inner;
        _predicate = predicate;
        Advance();
    }

    public bool HasNext() => _nextItem != null;
    public FileMetadata Next()
    {
        var current = _nextItem!;
        Advance();
        return current;
    }

    public void Reset() { _inner.Reset(); Advance(); }

    private void Advance()
    {
        _nextItem = null;
        while (_inner.HasNext())
        {
            var candidate = _inner.Next();
            if (_predicate(candidate)) { _nextItem = candidate; return; }
        }
    }
}
```

**Step 5: Client code — uniform for ANY collection**

```csharp
// Same code works with array, list, tree, DB cursor — anything!
void PrintAll(IFileIterator iterator)
{
    while (iterator.HasNext())
    {
        var file = iterator.Next();
        Console.WriteLine($"  {file.FileName} by {file.Author}");
    }
}

IFileCollection collection = new ArrayFileCollection(10);
// ... add files ...

PrintAll(collection.CreateIterator());  // all files
PrintAll(collection.CreateFilteredIterator(f => f.Author == "Alice"));  // only Alice's

// Multiple independent iterators
var iter1 = collection.CreateIterator();
var iter2 = collection.CreateIterator();
iter1.Next(); iter1.Next();  // advances iter1
iter2.Next();                // iter2 is independent — at position 1
```

---

## When to Use Iterator

### Use Iterator When:

| Scenario | Why Iterator Helps |
|----------|-------------------|
| Collection's internal structure must be hidden | Iterator exposes only HasNext/Next |
| Multiple data structures, same traversal API | Uniform interface regardless of backing store |
| Need multiple independent traversals simultaneously | Each CreateIterator() returns independent cursor |
| Filtered/transformed traversal | Compose FilteredIterator on top of any iterator |
| Lazy/paginated data (DB cursor, API pagination) | Iterator loads next page only when needed |
| Traversal algorithms vary (DFS, BFS, in-order) | Different iterators for same collection |

### Don't Use Iterator When:

| Scenario | Why Not |
|----------|---------|
| Simple list with `foreach` support (.NET `IEnumerable`) | Language already provides iterators |
| Only one traversal order ever needed | Direct loop is simpler |
| Collection is tiny (5 items) | Over-engineering |
| Random access is primary use case | Iterator is sequential by design |

### Iterator in .NET (built-in):

C# has iterator support built into the language via `IEnumerable<T>` / `IEnumerator<T>` and `yield return`:

```csharp
public class FileCollection : IEnumerable<FileMetadata>
{
    private readonly List<FileMetadata> _files = new();

    public IEnumerator<FileMetadata> GetEnumerator()
    {
        foreach (var file in _files)
            yield return file;  // compiler generates the iterator state machine
    }

    // Filtered iterator via LINQ (built on IEnumerable)
    public IEnumerable<FileMetadata> GetByAuthor(string author)
        => _files.Where(f => f.Author == author);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

The manual Iterator pattern is still relevant for:
- Custom traversal logic (BFS/DFS on trees)
- Paginated API cursors (load next page on HasNext)
- Complex state machines during traversal
- Non-.NET environments or interview problems

---

## LLD Problems Where Iterator Applies

| Problem | Collection Structure | Iterator Variants |
|---------|---------------------|-------------------|
| **Playlist (Music App)** | Linked list of songs | SequentialIterator, ShuffleIterator, RepeatIterator |
| **File System Browser** | Tree (directories + files) | DFS iterator, BFS iterator, files-only iterator |
| **Social Media Feed** | Paginated API responses | PaginatedIterator (loads next page on demand) |
| **Database Result Set** | DB cursor (server-side) | ForwardOnlyIterator, ScrollableIterator |
| **Notification Inbox** | Priority queue | HighPriorityFirst, ChronologicalIterator |
| **Shopping Cart** | Map/Dictionary of items | AllItems, FilterByCategory, SortByPrice |
| **Chat History** | Time-ordered messages | NewestFirst, OldestFirst, UnreadOnly |
| **Book Library** | Categorized shelves (tree) | ByAuthor, ByGenre, ByYear, AllBooks |
| **Image Gallery** | Grid/album structure | ByDate, ByLocation, FavoritesOnly |
| **Undo History (Editor)** | Stack of commands | ForwardIterator, ReverseIterator |

### Example: Paginated API Iterator (Lazy Loading)

```csharp
public class PaginatedFileIterator : IFileIterator
{
    private readonly IStorageApi _api;
    private readonly int _pageSize;
    private List<FileMetadata> _currentPage = new();
    private int _posInPage;
    private string? _nextPageToken;
    private bool _exhausted;

    public PaginatedFileIterator(IStorageApi api, int pageSize = 50)
    {
        _api = api;
        _pageSize = pageSize;
        LoadNextPage();
    }

    public bool HasNext()
    {
        if (_posInPage < _currentPage.Count) return true;
        if (_exhausted) return false;
        LoadNextPage();
        return _currentPage.Count > 0;
    }

    public FileMetadata Next() => _currentPage[_posInPage++];

    private void LoadNextPage()
    {
        var result = _api.ListFiles(_pageSize, _nextPageToken);
        _currentPage = result.Files;
        _nextPageToken = result.NextToken;
        _posInPage = 0;
        _exhausted = _nextPageToken == null;
    }

    public void Reset() { _nextPageToken = null; _exhausted = false; LoadNextPage(); }
}

// Client doesn't know about pagination — just uses HasNext()/Next()
var iter = new PaginatedFileIterator(s3Api, pageSize: 100);
while (iter.HasNext())
    Process(iter.Next()); // transparently loads pages as needed
```

### Signals in LLD interviews:

1. "Traverse a collection without exposing internals"
2. "Support multiple traversal strategies (DFS, BFS, sorted, filtered)"
3. "Paginated results / lazy loading"
4. "Multiple simultaneous cursors on same data"
5. "Uniform iteration over heterogeneous data structures"
