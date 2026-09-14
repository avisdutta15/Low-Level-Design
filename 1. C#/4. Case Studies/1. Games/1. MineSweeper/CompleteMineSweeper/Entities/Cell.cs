using MineSweeper.Enums;

namespace MineSweeper.Entities;

/// <summary>
/// Represents a single cell on the minesweeper board.
/// Thread-safe: state mutations are protected by a lock.
/// </summary>
public sealed class Cell
{
    private readonly object _cellLock = new object();

    public int Row { get; }
    public int Column { get; }
    public bool IsMine { get; private set; }
    public int AdjacentMineCount { get; private set; }
    public CellState State { get; private set; }

    public Cell(int row, int column)
    {
        Row = row;
        Column = column;
        State = CellState.Hidden;
    }

    public void PlaceMine()
    {
        lock (_cellLock)
        {
            IsMine = true;
        }
    }

    public void RemoveMine()
    {
        lock (_cellLock)
        {
            IsMine = false;
        }
    }

    public void SetAdjacentMineCount(int count)
    {
        lock (_cellLock)
        {
            AdjacentMineCount = count;
        }
    }

    /// <summary>
    /// Reveals the cell. Returns false if the cell is flagged or already revealed.
    /// </summary>
    public bool Reveal()
    {
        lock (_cellLock)
        {
            if (State != CellState.Hidden) return false;
            State = CellState.Revealed;
            return true;
        }
    }

    /// <summary>
    /// Toggles the flag on a hidden cell. Flagged cells become hidden, hidden cells become flagged.
    /// </summary>
    public bool ToggleFlag()
    {
        lock (_cellLock)
        {
            if (State == CellState.Revealed) return false;
            State = State == CellState.Flagged ? CellState.Hidden : CellState.Flagged;
            return true;
        }
    }
}