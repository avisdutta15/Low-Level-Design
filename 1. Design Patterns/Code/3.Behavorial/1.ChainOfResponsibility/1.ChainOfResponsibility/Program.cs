public enum LogLevel
{
    DEBUG = 0,
    INFO = 1,
    WARNING = 2,
    ERROR = 3,
    CRITICAL = 4
}

//Log message structure
public class LogMessage
{
    public DateTime TimeStamp {get; set;}
    public LogLevel Level {get; set;}
    public string Message{get; set;}
    public string Source{get; set;}
    public Exception? Exception{get; set;}

    public LogMessage(LogLevel level, string message, string? source = null, Exception? exception = null)
    {
        TimeStamp = DateTime.Now;
        Level = level;
        Message = message;
        Source = source ?? "Unknown";
        Exception = exception;
    }

    //To structure the LogMessage, override the ToString Method
    public override string ToString()
    {
        return $"[{TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] [{Source}] {Message}";
    }
}

//Handler Interface (Optional)
public interface ILogHandler
{
    ILogHandler SetNext(ILogHandler nextHandler);
    void Handle(LogMessage message);
}

//Handler Abstract class implementing the Handler Interface and adding few more methods
public abstract class LogHandler : ILogHandler
{
    protected ILogHandler? _nextHandler;
    protected LogLevel _minimumLogLevel;        // Minimum level this handler processes
    
    public LogHandler(LogLevel minimumLevel = LogLevel.DEBUG)
    {
        _nextHandler = null;
        _minimumLogLevel = minimumLevel;
    }
    
    public ILogHandler SetNext(ILogHandler nextHandler)
    {
        _nextHandler = nextHandler;
        return nextHandler;                     // For fluent setup
    }
    public void Handle(LogMessage message)
    {
        if(message.Level >= _minimumLogLevel && ShouldHandle(message))
        {
            ProcessMessage(message);
        }
        
        // Always pass to next handler (all handlers get a chance)
        if(_nextHandler != null)
        {
            _nextHandler.Handle(message);
        }
    }    

    protected abstract bool ShouldHandle(LogMessage message);
    protected abstract void ProcessMessage(LogMessage message);
}

//Concrete Handlers
public class ConsoleLogger : LogHandler
{
    public ConsoleLogger(LogLevel minimumLevel = LogLevel.INFO) : base(minimumLevel){}
    protected override bool ShouldHandle(LogMessage message)
    {
        return true;
    }
    protected override void ProcessMessage(LogMessage message)
    {
        Console.WriteLine($"Logging to Console: {message.ToString()}");
    }
}

public class FileLogger : LogHandler, IDisposable
{
    private readonly string _logDirectory;
    private string? _currentFilePath;
    private StreamWriter? _currentWriter;
    public FileLogger(string logDirectory = "./logs", LogLevel minimumLevel = LogLevel.DEBUG) : base(minimumLevel)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        OpenNewFile();
    }
    protected override bool ShouldHandle(LogMessage message)
    {
        return true;
    }

    protected override void ProcessMessage(LogMessage message)
    {
        Console.WriteLine($"Logging to File: {message.ToString()}");
        _currentWriter?.WriteLine(message.ToString());
        _currentWriter?.Flush();
    }   

    private void OpenNewFile()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _currentFilePath = Path.Combine(_logDirectory, $"app_{timestamp}.log");
        _currentWriter = new StreamWriter(_currentFilePath, append: true);
    }

    public void Dispose()
    {
        _currentWriter?.Close();
        _currentWriter?.Dispose();
    }
}

public class DatabaseLogger : LogHandler
{
    private string _connectionString;
    public DatabaseLogger(string connectionString, LogLevel minimumLevel = LogLevel.WARNING) : base(minimumLevel)
    {
        _connectionString = connectionString;
    }

    //Log only Warnings
    protected override bool ShouldHandle(LogMessage message)
    {
        return _minimumLogLevel == LogLevel.WARNING;
    }

    protected override void ProcessMessage(LogMessage message)
    {
        Console.WriteLine($"Logging to Database: {message.ToString()}");
    }    
}

//Utility class used by the Client to create the chain of Concrete Handlers and send the request to the first Concrete Handler
public class LoggingFramework
{
    private ILogHandler? _firstLogHandler = null;
    private ILogHandler? _lastLogHandler = null;

    public LoggingFramework AddHandler(ILogHandler handler)
    {
        if(_firstLogHandler == null)
        {
            _firstLogHandler = handler;
            _lastLogHandler  = handler;
        }
        else
        {
            _lastLogHandler?.SetNext(handler);
            _lastLogHandler = handler;
        }
        return this;                    // For fluent configuration
    }
    public void Log(LogMessage message)
    {
        _firstLogHandler?.Handle(message);
    }

    // Convenience methods
    public void Debug(string message, string? source = null)
    {
        Log(new LogMessage(LogLevel.DEBUG, message, source, null));
    }
    
    public void Info(string message, string? source = null)
    {
        Log(new LogMessage(LogLevel.INFO, message, source, null));
    }
    
    public void Warning(string message, string? source = null, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.WARNING, message, source, ex));
    }
    
    public void Error(string message, string? source = null, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.ERROR, message, source, ex));
    }
    
    public void Critical(string message, string? source = null, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.CRITICAL, message, source, ex));
    }
}

//Client
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Logging Framework with Chain of Responsibility ===\n");
        // Build the logging chain
        var logger = new LoggingFramework()
                            .AddHandler(new ConsoleLogger(LogLevel.INFO))
                            .AddHandler(new FileLogger("./logs", LogLevel.WARNING))
                            .AddHandler(new DatabaseLogger("Server=localhost;", LogLevel.WARNING));

        Console.WriteLine("\n--- Testing Info Handling ---");
        logger.Info("Processing order #12345", "OrderProcessor");

        Console.WriteLine("\n--- Testing Error Handling ---");
        try
        {
            throw new InvalidOperationException("Database connection failed");
        }
        catch (Exception ex)
        {
            //This will log into all the 3 loggers - Console, File, Database
            logger.Error("Failed to process user request", "OrderService", ex);
        }

        Console.WriteLine("\n--- Testing Warning Handling ---");
        logger.Warning("Cache miss for key 'user_profile_123'", "CacheService");
    }
}

/*
    Implementation Notes:
    The Chain of Responsibility Design Pattern consists of the following components:
    Handler : The handler is an interface or an abstract class that defines a method for handling requests. 
              It may also include a reference to the next handler in the chain.

    ConcreteHandler : The concrete handler is a class that implements the handler interface. 
                      It contains the actual implementation for handling requests. If it cannot handle a request, 
                      it passes the request to the next handler in the chain.  Each request handler(except the last handler in chain) 
                      must have reference to the next handler in chain.

    Client : The client is responsible for creating the chain of handlers and initiating the request. The client sends 
             the request to the first handler in the chain.
             Client doesn’t know which request handler will process the request and it will just send the request to the first handler in the chain.
*/