using MineSweeper.Strategies;
using MineSweeper.Enums;
using MineSweeper.Strategies;

namespace MineSweeper.Entities;

/// <summary>
/// Manages the grid of cells, mine placement, and adjacency calculation.
/// Delegates reveal behavior to an IRevealStrategy.
/// </summary>
public sealed class Board
{
    private readonly Cell[,] _grid;
    private readonly int _rows;
    private readonly int _columns;
    private readonly int _mineCount;
    private readonly IRevealStrategy _revealStrategy;
    private bool _minesPlaced;

    // Offsets for all 8 neighbors
    private static readonly (int dr, int dc)[] Neighbors =
    {
        (-1, -1), (-1, 0), (-1, 1),
        ( 0, -1),          ( 0, 1),
        ( 1, -1), ( 1, 0), ( 1, 1)
    };

    public int Rows => _rows;
    public int Columns => _columns;
    public int MineCount => _mineCount;

    public Board(DifficultyConfig config, IRevealStrategy? revealStrategy = null)
    {
        _rows = config.Rows;
        _columns = config.Columns;
        _mineCount = config.MineCount;
        _revealStrategy = revealStrategy ?? new CascadeRevealStrategy();    //default reveal strategy
        _grid = new Cell[_rows, _columns];

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _columns; c++)
                _grid[r, c] = new Cell(r, c);
    }

    public Cell GetCell(int row, int col)
    {
        return _grid[row, col];
    }

    public bool IsInBounds(int row, int col)
    {
        return row >= 0 && row < _rows && col >= 0 && col < _columns;
    }

    /// <summary>
    /// Places mines randomly, ensuring the first-click cell and its neighbors are safe.
    /// </summary>
    public void PlaceMines(int safeRow, int safeCol)
    {
        if (_minesPlaced) return;

        var rng = Random.Shared;
        //Keep a hash set of all the neighbours of the safe row and column
        var safeZone = new HashSet<(int, int)>();
        safeZone.Add((safeRow, safeCol));
        
        foreach (var (dr, dc) in Neighbors)
        {
            int nr = safeRow + dr, nc = safeCol + dc;
            if (IsInBounds(nr, nc)) safeZone.Add((nr, nc));
        }

        //while the countOfMinesPlaced < minecount
        //take a random row and column and try to place mine (validate if withinbounds && !toggled && !inSafeZone)
        int countOfMinesPlaced = 0;
        while (countOfMinesPlaced < _mineCount)
        {
            int r = rng.Next(0, _rows);
            int c = rng.Next(0, _columns);
            if (safeZone.Contains((r, c)) || _grid[r, c].IsMine)
                continue;
            _grid[r, c].PlaceMine();
            countOfMinesPlaced++;
        }

        //Now CalculateAdjacentMines for each cell.
        CalculateAdjacentMineCounts();
        _minesPlaced = true;
    }

    private void CalculateAdjacentMineCounts()
    {
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                if (_grid[r, c].IsMine) continue;
                int count = 0;
                foreach (var (dr, dc) in Neighbors)
                {
                    int nr = r + dr, nc = c + dc;
                    if (IsInBounds(nr, nc) && _grid[nr, nc].IsMine) count++;
                }
                _grid[r, c].SetAdjacentMineCount(count);
            }
        }
    }

    /// <summary>
    /// Delegates cell reveal to the configured IRevealStrategy.
    /// </summary>
    public List<Cell> RevealCell(int row, int col)
    {
        return _revealStrategy.Reveal(this, row, col);
    }

    /// <summary>
    /// Returns true when every non-mine cell has been revealed.
    /// </summary>
    public bool AreAllNonMinesRevealed()
    {
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _columns; c++)
                if (_grid[r, c].IsMine == false && _grid[r, c].State != CellState.Revealed)
                    return false;
        return true;
    }

    /// <summary>
    /// Reveals all mines on the board (used on game loss).
    /// </summary>
    public void RevealAllMines()
    {
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _columns; c++)
                if (_grid[r, c].IsMine)
                    _grid[r, c].Reveal();
    }
}
