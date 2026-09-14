namespace MineSweeper.Entities;

/// <summary>
/// Thread-safe statistics tracker across multiple games.
/// </summary>
public sealed class GameStatistics
{
    private int _gamesPlayed;
    private int _wins;
    private int _losses;
    private readonly object _lock = new();

    public int GamesPlayed { 
        get { lock (_lock) return _gamesPlayed; } 
    }

    public int Wins { 
        get { lock (_lock) return _wins; } 
    }

    public int Losses { 
        get { lock (_lock) return _losses; } 
    }

    public void RecordWin()
    {
        lock (_lock) { 
            _gamesPlayed++; 
            _wins++; 
        }
    }

    public void RecordLoss()
    {
        lock (_lock) { 
            _gamesPlayed++; 
            _losses++; 
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
