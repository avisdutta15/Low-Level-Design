namespace MineSweeper.Observers;

/// <summary>
/// Thread-safe statistics tracker across multiple games.
/// </summary>
public sealed class GameStatistics : IGameObserver
{
    private int _gamesPlayed;
    private int _wins;
    private int _losses;
    private readonly object _lock = new();

    public int GamesPlayed
    {
        get { lock (_lock) return _gamesPlayed; }
    }

    public int Wins
    {
        get { lock (_lock) return _wins; }
    }

    public int Losses
    {
        get { lock (_lock) return _losses; }
    }

    public void OnGameEvent(GameEventType eventType)
    {
        lock (_lock)
        {
            switch (eventType)
            {
                case GameEventType.GameWon:
                    _gamesPlayed++;
                    _wins++;
                    break;
                case GameEventType.GameLost:
                    _gamesPlayed++;
                    _losses++;
                    break;
            }
        }
    }


    public override string ToString()
    {
        lock (_lock)
        {
            return $"Games: {_gamesPlayed} | Wins: {_wins} | Losses: {_losses}";
        }
    }
}
