namespace MineSweeper.Observers;
/// <summary>
/// Observer interface for game events.
/// Implementations receive notifications when game state changes occur.
/// </summary>
public interface IGameObserver
{
    void OnGameEvent(GameEventType eventType);
}
