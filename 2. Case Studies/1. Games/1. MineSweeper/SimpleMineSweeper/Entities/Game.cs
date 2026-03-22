using MineSweeper.Enums;

namespace MineSweeper.Entities;

/// <summary>
/// Orchestrates a single minesweeper game session.
/// Coordinates between the Board and tracks game state transitions.
/// </summary>
public sealed class Game
{
    private readonly Board _board;
    private readonly GameStatistics _statistics;
    private readonly object _lock = new();

    public GameState State { get; private set; }
    public Board Board => _board;

    public Game(Difficulty difficulty, GameStatistics statistics)
    {
        var config = DifficultyConfig.GetConfig(difficulty);
        _board = new Board(config);
        _statistics = statistics;
        State = GameState.NotStarted;
    }

    /// <summary>
    /// Reveals a cell at (row, col). On first click, mines are placed ensuring safety.
    /// Returns the list of cells revealed, or empty if the move was invalid.
    /// </summary>
    public List<Cell> Reveal(int row, int col)
    {
        lock (_lock)
        {
            // If the game is already won or lost, return empty list of cells
            if (State == GameState.Won || State == GameState.Lost)
                return new List<Cell>();

            // Check if the row and column are valid
            if (_board.IsInBounds(row, col) == false)
                return new List<Cell>();

            // Check if the cell is flagged. If flagged then we cannot do any reveal.
            // simply return 
            var cell = _board.GetCell(row, col);
            if (cell.State == CellState.Flagged)
                return new List<Cell>();

            // First click: place mines, guaranteeing this cell is safe.
            // After first click, Change the state of the Game : NotStarted->InProgress
            if (State == GameState.NotStarted)
            {
                _board.PlaceMines(row, col);
                State = GameState.InProgress;
            }

            //Reveal the cell from the board.
            //Revealing a cell also reveals the neighbouring cells with 0 mine count
            var revealed = _board.RevealCell(row, col);

            // Check loss
            // Loss Condition: If the cell is a Mine then game is lost
            // Set game state to Lost. Reveal All the Mines.
            if (cell.IsMine)
            {
                State = GameState.Lost;
                _board.RevealAllMines();
                _statistics.RecordLoss();
                return revealed;
            }

            // Check win
            // Win Condition: If AllNonMinesAreRevealed
            if (_board.AreAllNonMinesRevealed() == true)
            {
                State = GameState.Won;
                _statistics.RecordWin();
            }

            return revealed;
        }
    }

    /// <summary>
    /// Toggles the flag on a cell. Returns true if the toggle succeeded.
    /// </summary>
    public bool ToggleFlag(int row, int col)
    {
        lock (_lock)
        {
            // If the game is already won or lost, return false
            if (State == GameState.Won || State == GameState.Lost)
                return false;

            // Check if the row and column are valid
            if (!_board.IsInBounds(row, col))
                return false;

            // Check if able to toggle. If able then toggle and return true.
            // else false.
            return _board.GetCell(row, col).ToggleFlag();
        }
    }
}