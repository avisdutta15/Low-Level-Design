using MineSweeper.Entities;

namespace MineSweeper.Strategies;

/// <summary>
/// Strategy interface for cell reveal behavior.
/// Allows swapping reveal algorithms without modifying the Board.
/// </summary>
public interface IRevealStrategy
{
    /// <summary>
    /// Reveals the cell at (row, col) and returns all cells that were revealed.
    /// </summary>
    List<Cell> Reveal(Board board, int row, int col);
}