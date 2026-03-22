namespace MineSweeper.Observers;

/// <summary>
/// Events that observers can react to.
/// </summary>
public enum GameEventType
{
    GameStarted,
    CellRevealed,
    CellFlagged,
    GameWon,
    GameLost
}
