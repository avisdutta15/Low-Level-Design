using Iterator.V2;

// =============================================================================
// V2: HOW TO IMPLEMENT ITERATOR PATTERN
// =============================================================================

Console.WriteLine("=== Iterator Pattern: Uniform traversal regardless of structure ===");
Console.WriteLine();

// --- Array-backed collection ---
IFileCollection arrayCollection = new ArrayFileCollection(10);
arrayCollection.Add(new FileMetadata { FileName = "report.pdf", SizeBytes = 1024, Author = "Alice" });
arrayCollection.Add(new FileMetadata { FileName = "data.csv", SizeBytes = 2048, Author = "Bob" });
arrayCollection.Add(new FileMetadata { FileName = "image.png", SizeBytes = 4096, Author = "Alice" });
arrayCollection.Add(new FileMetadata { FileName = "notes.txt", SizeBytes = 512, Author = "Charlie" });

// --- List-backed collection ---
IFileCollection listCollection = new ListFileCollection();
listCollection.Add(new FileMetadata { FileName = "backup.zip", SizeBytes = 8192, Author = "Dave" });
listCollection.Add(new FileMetadata { FileName = "log.txt", SizeBytes = 256, Author = "Eve" });

// Same client code works with BOTH collections — doesn't know internal structure
Console.WriteLine("--- Traverse array collection (same API) ---");
PrintAll(arrayCollection.CreateIterator());

Console.WriteLine();
Console.WriteLine("--- Traverse list collection (same API) ---");
PrintAll(listCollection.CreateIterator());

Console.WriteLine();
Console.WriteLine("--- Filtered iterator: only Alice's files ---");
var aliceFiles = arrayCollection.CreateFilteredIterator(f => f.Author == "Alice");
PrintAll(aliceFiles);

Console.WriteLine();
Console.WriteLine("--- Filtered iterator: files > 1KB ---");
var largeFiles = arrayCollection.CreateFilteredIterator(f => f.SizeBytes > 1024);
PrintAll(largeFiles);

Console.WriteLine();
Console.WriteLine("--- Multiple independent iterators on same collection ---");
var iter1 = arrayCollection.CreateIterator();
var iter2 = arrayCollection.CreateIterator();
Console.WriteLine($"  iter1.Next(): {iter1.Next().FileName}");
Console.WriteLine($"  iter1.Next(): {iter1.Next().FileName}");
Console.WriteLine($"  iter2.Next(): {iter2.Next().FileName}"); // independent — starts from beginning
Console.WriteLine("  (Two iterators, independent positions!)");

Console.WriteLine();
Console.WriteLine("=== Benefits ===");
Console.WriteLine("1. UNIFORM API: Same HasNext()/Next() for array, list, tree, DB cursor");
Console.WriteLine("2. HIDDEN INTERNALS: Client never sees the internal data structure");
Console.WriteLine("3. SWAPPABLE: Change from array to tree — client code unchanged");
Console.WriteLine("4. MULTIPLE ITERATORS: Independent cursors on the same collection");
Console.WriteLine("5. FILTERED ITERATION: Compose iterators (FilteredFileIterator wraps any iterator)");
Console.WriteLine("6. LAZY: Can iterate over paginated/streamed data without loading everything");

// Helper method — works with ANY IFileIterator
static void PrintAll(IFileIterator iterator)
{
    while (iterator.HasNext())
    {
        var file = iterator.Next();
        Console.WriteLine($"  {file.FileName} ({file.SizeBytes} bytes) by {file.Author}");
    }
}
