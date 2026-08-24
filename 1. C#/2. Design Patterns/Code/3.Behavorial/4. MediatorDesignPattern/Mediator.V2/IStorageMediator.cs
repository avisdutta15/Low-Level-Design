namespace Mediator.V2;

/// <summary>
/// Mediator interface — all components communicate through this.
/// Components never call each other directly.
/// </summary>
public interface IStorageMediator
{
    void Notify(object sender, string eventType, Dictionary<string, object>? data = null);
}
