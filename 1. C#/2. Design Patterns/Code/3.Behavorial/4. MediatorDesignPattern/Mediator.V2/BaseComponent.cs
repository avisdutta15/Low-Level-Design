namespace Mediator.V2;

/// <summary>
/// Base class for all components — holds a reference to the mediator.
/// Components only know the mediator, not each other.
/// </summary>
public abstract class BaseComponent
{
    protected IStorageMediator Mediator { get; }

    protected BaseComponent(IStorageMediator mediator)
    {
        Mediator = mediator;
    }
}
