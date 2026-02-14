public interface IText
{
    string GetContent();
    string GetDescription();
}

// Base implementation
public class PlainText : IText
{
    private string _content;

    public PlainText(string content) => _content = content;

    public string GetContent() => _content;
    public string GetDescription() => "Plain Text";
}

/*
        IText plainText = new PlainText("Hello");
        ITextDecorator boldPlainText = new BoldDecorator(plainText);
        ITextDecorator italicsDecorator = new ItalicsDecorator(boldPlainText);
 */


public abstract class TextDecorator : IText
{
    // This is the key: holds a reference to the text we're decorating
    private IText _baseText;
    public TextDecorator(IText baseText)
    {
        _baseText = baseText; // Store the reference
    }

    // Default implementation: delegate to the wrapped text
    public virtual string GetContent()
    {
        return _baseText.GetContent();
    }

    // Default implementation: delegate to the wrapped text
    public virtual string GetDescription()
    {
        return _baseText.GetDescription();
    }
}

public class BoldDecorator : TextDecorator
{
    //Pass the concrete class to the abstract class. 
    public BoldDecorator(IText text) : base(text)
    {
    }

    public override string GetContent()
    {
        return $"<b> {base.GetContent()} </b>";
    }

    public override string GetDescription()
    {
        return $"Bold, {base.GetContent()}";
    }
}

public class ItalicDecorator : TextDecorator
{
    public ItalicDecorator(IText text) : base(text)
    {
    }

    public override string GetContent()
    {
        return $"<i> {base.GetContent()} </i>";
    }

    public override string GetDescription()
    {
        return $"Italic, {base.GetContent()}";
    }
}

public class DecoratorDemo()
{
    public static void Main()
    {
        IText text = new PlainText("Hello World");
        TextDecorator boldText = new BoldDecorator(text);
        TextDecorator italiBoldDecorator = new ItalicDecorator(boldText);
        Console.WriteLine($"{italiBoldDecorator.GetContent()}");

        TextDecorator boldItalicText = new BoldDecorator(new ItalicDecorator(text));
        Console.WriteLine($"{boldItalicText.GetContent()}");

        Tests();
    }

    public static async Task Tests()
    {
        // Pizza System Usage
        IPizza pizza = new CheeseDecorator(new PepperoniDecorator(new Margherita()));
        Console.WriteLine($"{pizza.GetDescription()} - ${pizza.GetCost()}");

        // Notification System Usage  
        INotification notification = new PushDecorator(new SMSDecorator(new EmailDecorator(new BasicNotification())));
        notification.Send("Hello World!");

        // File Processing Usage
        IFileProcessor processor = new LoggingDecorator(new EncryptionDecorator(new CompressionDecorator(new BasicFileProcessor())));
        processor.Process("document.txt");

        // Web Middleware Usage
        IMiddleware pipeline = new CompressionMiddleware(new LoggingMiddleware(new AuthenticationMiddleware(new BaseMiddleware())));
        await pipeline.Handle(new HttpContext());

        // Game Character Usage
        ICharacter character = new ShieldDecorator(new SwordDecorator(new FireAbilityDecorator(new BasicCharacter())));
        Console.WriteLine($"{character.GetDescription()} - Attack: {character.GetAttack()}, Defense: {character.GetDefense()}");
    }
}

/* 
 * Decorator is a structural design pattern that lets you attach new behaviors to objects by placing these objects 
 * inside special wrapper objects that contain the behaviors.
 * 
 * Suppose we have a plain text that we want to decorate with bold, italics, underline.
 * so we create boldtext, italicstext, underline class. All deriving from concrete component's interface.
 * now we want combinations, bolditalictext, italicboldtext, boldunderlinetext, underlineboldtext etc...
 * You see we are keep on adding classes if we want combinations.
 * Also if we add a new feature strikethrough, then it adds new combination classes.
 * 
 * So we wrap the base concrete component with decorators (bold, italic, underline)
 *  
 * 3 Steps:
 * 1. Create an interface that has all the methods of Concrete Class i.e. the last class (component) in the chain on which we want to 
 *    decorate.
 * 2. Create an abstract class BaseDecorator and inherit from the IConcreteComponent
 *    The base decorator holds a reference of the concrete object.
 *    The communication between the concrete decorators and the concrete component happens with this reference object.
 *    The constructor of the abstract class sets this reference.
 * 3. Create concrete decorators by deriving from BaseDecorator.
 *    Due to this inheritance, we inherited the concrete class methods from BaseDecorator.
 *    Now we can either modify the inherited concrete class methods
 *    or we can add extra methods that will be called from these methods.
 *    decorator{
 *      baseclassmethod(){
 *          base.baseclassmethod();
 *          extra_method();
 *      }
 *      extra_method(){}
 *    }
 * 
 * 
 * 
 *  Abstract Class: The base decorator contains a text object instead of inheriting from it. (Composition over Inheritance)
 *                  We don't instantiate TextDecorator directly. 
 *                  It provides common structure for all concrete decorators. It passes the methods of Concrete Component.
 *                  By default, the decorator just passes calls through to the wrapped object.
 *                  Concrete decorators will override this behavior.
 *                 
 * 
 *  TextDecorator boldItalicText = new BoldDecorator(new ItalicDecorator(text));
 *  Console.WriteLine($"{boldItalicText.GetContent()}");
 * 
 *  When we call boldItalicText.GetContent()
 *      1. BoldDecorator.GetContent() calls _text.GetContent() which is (ItalicDecorator)
 *      2. ItalicDecorator.GetContent() calls _text.GetConte() which is (PlainText)
 *      3. PlainText.GetContent() returns "Hello World"
 *      4. ItalicDecorator wraps it with : <i>Hello World</i>
 *      5. BoldDecorator wraps it with   : <b><i>Hello World</i></b>
 *      
 *  Wraps an existing IText object 
 *  Adds its specific formatting in GetContent() 
 *  Updates the description in GetDescription() 
 *  Maintains the IText interface - client code doesn't know it's dealing with a decorator.
 * 
 *  Key Design Principles Demonstrated
 *  1. Open/Closed Principle
 *  csharp
 *  // OPEN for extension
 *  public class StrikethroughDecorator : TextDecorator
 *  {
 *      public StrikethroughDecorator(IText text) : base(text) { }
 *      
 *      public override string GetContent() => $"<s>{_text.GetContent()}</s>";
 *      public override string GetDescription() => _text.GetDescription() + ", Strikethrough";
 *  }
 *  
 *  // CLOSED for modification - no need to change existing classes!
 *  2. Single Responsibility Principle
 *  PlainText: Only stores and returns plain text
 *  
 *  BoldDecorator: Only adds bold formatting
 *  
 *  ItalicDecorator: Only adds italic formatting
 *  
 *  Each class has one reason to change
 *  
 *  3. Liskov Substitution Principle
 *  csharp
 *  // All these can be used interchangeably
 *  IText text1 = new PlainText("Hello");
 *  IText text2 = new BoldDecorator(text1); 
 *  IText text3 = new ItalicDecorator(text2);
 *  
 *  // Client code works with IText interface, doesn't care about concrete types
 *  4. Composition Over Inheritance
 *  csharp
 *  // We use COMPOSITION (has-a) instead of INHERITANCE (is-a)
 *  public class BoldDecorator : TextDecorator
 *  {
 *      protected IText _text;  // COMPOSITION - has a text object
 *      
 *      // Not: public class BoldText : PlainText (INHERITANCE)
 *  }
 * 
 * 
 * 
 *  LLD UseCase:
 *  Pizza/Coffee/Order System - Adding toppings/ingredients
 *  Notification System - Adding channels (Email, SMS, Push)
 *  File Processing - Adding compression, encryption, logging
 *  Web Framework - Middleware pipeline
 *  Game Development - Adding character abilities/equipment
 * 
*/