// 1. Handler Interface
public interface IHandler
{
    IHandler SetNext(IHandler handler);
    public object Handle(object request);
}

// 2. Abstract Base Handler
public abstract class AbstractHandler : IHandler
{
    private IHandler _nextHandler;

    public IHandler SetNext(IHandler handler)
    {
        _nextHandler = handler;
        return _nextHandler;    // Return handler for fluent chaining
    }

    public virtual object Handle(object request)
    {
        if (_nextHandler != null)
        {
            return _nextHandler.Handle(request);
        }
        return null;    // End of Chain
    }
}

// 3. Concrete Handler 1
public class ConcreteHandler1 : AbstractHandler
{
    public override object Handle(object request)
    {
        Console.WriteLine("Concrete Handler 1");
        return base.Handle(request);    //pass the request to the next handler
    }
}

// 4. Concrete Handler 2
public class ConcreteHandler2 : AbstractHandler
{
    public override object Handle(object request)
    {
        Console.WriteLine("Concrete Handler 2");
        return base.Handle(request);    //pass the request to the next handler
    }
}

// 5. Concrete Handler 3
public class ConcreteHandler3 : AbstractHandler
{
    public override object Handle(object request)
    {
        Console.WriteLine("Concrete Handler 3");
        return base.Handle(request);    //pass the request to the next handler
    }
}


public class ChainOfResponsiblityDemo
{
    public static void Main()
    {
        //Create concrete handlers
        IHandler handler1 = new ConcreteHandler1();
        IHandler handler2 = new ConcreteHandler2();
        IHandler handler3 = new ConcreteHandler3();

        //Set up the chain
        handler1.SetNext(handler2).SetNext(handler3);

        handler1.Handle("");
    }
}








/*
 *  Chain of Responsibility is a behavioral design pattern that lets you pass requests along a chain of handlers. 
 *  Upon receiving a request, each handler decides either to process the request or to pass it to the next handler 
 *  in the chain.
 *  
 *  The Chain of Responsibility relies on transforming particular behaviors into stand-alone objects called handlers.
*/