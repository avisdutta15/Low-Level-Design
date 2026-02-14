
// Component
public interface IMiddleware
{
    Task Handle(HttpContext context);
    string GetPipeline();
}

// Concrete Component
public class BaseMiddleware : IMiddleware
{
    public Task Handle(HttpContext context) => Task.CompletedTask;
    public string GetPipeline() => "Base";
}

// Base Decorator
public abstract class MiddlewareDecorator : IMiddleware
{
    protected IMiddleware _middleware;
    protected MiddlewareDecorator(IMiddleware middleware) => _middleware = middleware;

    public virtual Task Handle(HttpContext context) => _middleware.Handle(context);
    public virtual string GetPipeline() => _middleware.GetPipeline();
}

// Concrete Decorators
public class AuthenticationMiddleware : MiddlewareDecorator
{
    public AuthenticationMiddleware(IMiddleware middleware) : base(middleware) { }

    public override async Task Handle(HttpContext context)
    {
        Console.WriteLine("Authenticating request...");
        await _middleware.Handle(context);
    }

    public override string GetPipeline() => _middleware.GetPipeline() + ", Authentication";
}

public class LoggingMiddleware : MiddlewareDecorator
{
    public LoggingMiddleware(IMiddleware middleware) : base(middleware) { }

    public override async Task Handle(HttpContext context)
    {
        //Console.WriteLine($"Logging request: {context.Request.Path}");
        await _middleware.Handle(context);
        //Console.WriteLine($"Logging response: {context.Response.StatusCode}");
    }

    public override string GetPipeline() => _middleware.GetPipeline() + ", Logging";
}

public class CompressionMiddleware : MiddlewareDecorator
{
    public CompressionMiddleware(IMiddleware middleware) : base(middleware) { }

    public override async Task Handle(HttpContext context)
    {
        Console.WriteLine("Compressing response...");
        await _middleware.Handle(context);
    }

    public override string GetPipeline() => _middleware.GetPipeline() + ", Compression";
}

// Supporting class
public class HttpContext
{
    public object Request { get; set; }
    public object Response { get; set; }
}