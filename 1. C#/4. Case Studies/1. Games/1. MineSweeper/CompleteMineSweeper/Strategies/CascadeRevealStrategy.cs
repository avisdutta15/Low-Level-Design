using MineSweeper.Entities;
using MineSweeper.Strategies;

namespace MineSweeper.Strategies;

public sealed class CascadeRevealStrategy : IRevealStrategy
{
    // Offsets for all 8 neighbors
    private static readonly (int dr, int dc)[] Neighbors =
    {
        (-1, -1), (-1, 0), (-1, 1),
        ( 0, -1),          ( 0, 1),
        ( 1, -1), ( 1, 0), ( 1, 1)
    };

    public List<Cell> Reveal(Board board, int row, int col)
    {
        var revealed = new List<Cell>();
        var cell = board.GetCell(row, col);
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
                    if (!board.IsInBounds(nr, nc))
                        continue;

                    var neighbor = board.GetCell(nr, nc);
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
}
