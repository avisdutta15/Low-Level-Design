namespace Factory.V2;

/// <summary>
/// Product interface — defines what all notifications can do.
/// The client programs against this interface, never against concrete classes.
/// </summary>
public interface INotification
{
    void Send(string message);
}
