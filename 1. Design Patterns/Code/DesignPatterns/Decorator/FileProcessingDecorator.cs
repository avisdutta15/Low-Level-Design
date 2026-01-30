// Component
public interface IFileProcessor
{
    void Process(string filePath);
    string GetProcessingSteps();
}

// Concrete Component
public class BasicFileProcessor : IFileProcessor
{
    public void Process(string filePath) => Console.WriteLine($"Processing: {filePath}");
    public string GetProcessingSteps() => "Basic Processing";
}

// Base Decorator
public abstract class FileProcessorDecorator : IFileProcessor
{
    protected IFileProcessor _processor;
    protected FileProcessorDecorator(IFileProcessor processor) => _processor = processor;

    public virtual void Process(string filePath) => _processor.Process(filePath);
    public virtual string GetProcessingSteps() => _processor.GetProcessingSteps();
}

// Concrete Decorators
public class CompressionDecorator : FileProcessorDecorator
{
    public CompressionDecorator(IFileProcessor processor) : base(processor) { }

    public override void Process(string filePath)
    {
        Console.WriteLine($"Compressing: {filePath}");
        _processor.Process(filePath);
    }

    public override string GetProcessingSteps() => _processor.GetProcessingSteps() + ", Compression";
}

public class EncryptionDecorator : FileProcessorDecorator
{
    public EncryptionDecorator(IFileProcessor processor) : base(processor) { }

    public override void Process(string filePath)
    {
        Console.WriteLine($"Encrypting: {filePath}");
        _processor.Process(filePath);
    }

    public override string GetProcessingSteps() => _processor.GetProcessingSteps() + ", Encryption";
}

public class LoggingDecorator : FileProcessorDecorator
{
    public LoggingDecorator(IFileProcessor processor) : base(processor) { }

    public override void Process(string filePath)
    {
        Console.WriteLine($"[LOG] Start processing: {filePath}");
        _processor.Process(filePath);
        Console.WriteLine($"[LOG] Finished processing: {filePath}");
    }

    public override string GetProcessingSteps() => _processor.GetProcessingSteps() + ", Logging";
}