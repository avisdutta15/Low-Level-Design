namespace Template.V2;

/// <summary>
/// THE TEMPLATE METHOD: Defines the skeleton of the export algorithm.
/// 
/// - The overall workflow is FIXED (Connect → Validate → Transform → Write → Disconnect)
/// - Subclasses override specific steps (Connect, Transform, Write, Disconnect)
/// - Common logic (Validate) is implemented ONCE in the base class
/// - Subclasses CANNOT change the order of steps — only the details
/// 
/// The template method is Export() — it's not virtual, cannot be overridden.
/// Only the individual steps are abstract/virtual.
/// </summary>
public abstract class BaseDataExporter
{
    /// <summary>
    /// TEMPLATE METHOD — defines the algorithm skeleton.
    /// Final (non-virtual) — subclasses cannot change the workflow order.
    /// </summary>
    public void Export(string[] records)
    {
        Connect();

        if (!Validate(records))
            return;

        var transformed = Transform(records);
        Write(transformed);

        // Hook: optional post-write action (default does nothing)
        OnExportComplete(transformed.Length);

        Disconnect();
    }

    // --- Abstract steps: subclasses MUST implement these ---
    protected abstract void Connect();
    protected abstract string[] Transform(string[] records);
    protected abstract void Write(string[] transformedRecords);
    protected abstract void Disconnect();

    // --- Common step: implemented ONCE, shared by all subclasses ---
    protected virtual bool Validate(string[] records)
    {
        Console.WriteLine($"  [Base] Validating {records.Length} records...");
        if (records.Length == 0)
        {
            Console.WriteLine("  [Base] ERROR: No records to export");
            return false;
        }
        Console.WriteLine("  [Base] Validation passed");
        return true;
    }

    // --- Hook: optional step, subclasses can override if needed ---
    protected virtual void OnExportComplete(int recordCount)
    {
        // Default: do nothing. Subclasses can override for logging, metrics, etc.
    }
}
