using MineSweeper.Enums;

namespace MineSweeper.Entities;

/// <summary>
/// Manages the grid of cells, mine placement, adjacency calculation, and cascade reveal.
/// </summary>
public sealed class Board
{
    private readonly Cell[,] _grid;
    private readonly int _rows;
    private readonly int _columns;
    private readonly int _mineCount;
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

    public Board(DifficultyConfig config)
    {
        _rows = config.Rows;
        _columns = config.Columns;
        _mineCount = config.MineCount;
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
            if (IsInBounds(nr, nc))
                safeZone.Add((nr, nc));
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
    /// Reveals a cell. If it has zero adjacent mines, cascades to all neighbors (BFS flood fill).
    /// Returns the list of all cells revealed by this action.
    /// </summary>
    public List<Cell> RevealCell(int row, int col)
    {
        var revealed = new List<Cell>();
        var cell = _grid[row, col];
        if (!cell.Reveal()) return revealed;

        revealed.Add(cell);

        if (cell.AdjacentMineCount == 0 && !cell.IsMine)
        {
            //Now reveal all the neighbouring cells that has 0 AdjacentMineCount
            var queue = new Queue<(int, int)>();
            queue.Enqueue((row, col));

            while (queue.Count > 0)
            {
                var (cr, cc) = queue.Dequeue();
                foreach (var (dr, dc) in Neighbors)
                {
                    int nr = cr + dr, nc = cc + dc;
                    if (!IsInBounds(nr, nc))
                        continue;

                    var neighbor = _grid[nr, nc];
                    if (!neighbor.Reveal())
                        continue;

                    revealed.Add(neighbor);
                    if (neighbor.AdjacentMineCount == 0)
                        queue.Enqueue((nr, nc));
                }
            }
        }

        return revealed;
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
