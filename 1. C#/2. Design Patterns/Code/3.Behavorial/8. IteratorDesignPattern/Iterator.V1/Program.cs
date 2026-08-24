using Iterator.V1;

// =============================================================================
// V1: WHY DO WE NEED THE ITERATOR PATTERN?
// =============================================================================

Console.WriteLine("=== Without Iterator: Traversal code depends on internal structure ===");
Console.WriteLine();

// --- Array-based collection ---
var arrayCollection = new FileCollectionAsArray(10);
arrayCollection.Add(new FileMetadata { FileName = "report.pdf", SizeBytes = 1024, Author = "Alice" });
arrayCollection.Add(new FileMetadata { FileName = "data.csv", SizeBytes = 2048, Author = "Bob" });

Console.WriteLine("--- Array-based: must use index loop ---");
for (int i = 0; i < arrayCollection.Count; i++)
{
    var file = arrayCollection.GetAt(i);
    Console.WriteLine($"  {file.FileName} ({file.SizeBytes} bytes) by {file.Author}");
}

// --- List-based collection ---
var listCollection = new FileCollectionAsList();
listCollection.Add(new FileMetadata { FileName = "notes.txt", SizeBytes = 512, Author = "Charlie" });
listCollection.Add(new FileMetadata { FileName = "image.png", SizeBytes = 4096, Author = "Dave" });

Console.WriteLine();
Console.WriteLine("--- List-based: must use List<T> API ---");
foreach (var file in listCollection.GetAll())
{
    Console.WriteLine($"  {file.FileName} ({file.SizeBytes} bytes) by {file.Author}");
}

// --- LinkedList-based collection ---
var linkedCollection = new FileCollectionAsLinkedList();
linkedCollection.Add(new FileMetadata { FileName = "backup.zip", SizeBytes = 8192, Author = "Eve" });
linkedCollection.Add(new FileMetadata { FileName = "log.txt", SizeBytes = 256, Author = "Frank" });

Console.WriteLine();
Console.WriteLine("--- LinkedList-based: must follow Node.Next pointers ---");
var node = linkedCollection.Head;
while (node != null)
{
    Console.WriteLine($"  {node.Data.FileName} ({node.Data.SizeBytes} bytes) by {node.Data.Author}");
    node = node.Next;
}

Console.WriteLine();
Console.WriteLine("=== Problems ===");
Console.WriteLine("1. CLIENT KNOWS STRUCTURE: Array uses index, List uses foreach, LinkedList uses Node.Next");
Console.WriteLine("2. CHANGE BREAKS ALL: Switching from array to tree = rewrite every traversal loop");
Console.WriteLine("3. NO UNIFORM TRAVERSAL: Can't write ONE method that works with all 3 collections");
Console.WriteLine("4. EXPOSES INTERNALS: GetAll() returns the actual list — client can mutate it");
Console.WriteLine("5. NO MULTIPLE ITERATORS: Can't have two independent cursors on the same collection");
Console.WriteLine("6. NO FILTERED TRAVERSAL: Want only .pdf files? Must add logic in every loop");
