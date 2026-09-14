using MineSweeper.Entities;
using MineSweeper.Enums;

var stats = new GameStatistics();

while (true)
{
    Console.Clear();
    Console.WriteLine("=== MINESWEEPER ===");
    Console.WriteLine($"Stats: {stats}");
    Console.WriteLine();
    Console.WriteLine("Select difficulty:");
    Console.WriteLine("  1) Easy   (9x9,  10 mines)");
    Console.WriteLine("  2) Medium (16x16, 40 mines)");
    Console.WriteLine("  3) Hard   (30x16, 99 mines)");
    Console.WriteLine("  Q) Quit");
    Console.Write("> ");

    var input = Console.ReadLine()?.Trim().ToUpperInvariant();
    if (input == "Q") break;

    Difficulty difficulty = input switch
    {
        "1" => Difficulty.Easy,
        "2" => Difficulty.Medium,
        "3" => Difficulty.Hard,
        _ => Difficulty.Easy
    };

    var game = new Game(difficulty, stats);
    RunGame(game);
}

static void RunGame(Game game)
{
    while (game.State != GameState.Won && game.State != GameState.Lost)
    {
        Console.Clear();
        PrintBoard(game.Board);
        Console.WriteLine();
        Console.WriteLine("Commands:  r <row> <col>  (reveal)  |  f <row> <col>  (flag/unflag)");
        Console.Write("> ");

        var line = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(line)) continue;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) continue;

        if (!int.TryParse(parts[1], out int row) || !int.TryParse(parts[2], out int col))
            continue;

        switch (parts[0].ToLowerInvariant())
        {
            case "r":
                game.Reveal(row, col);
                break;
            case "f":
                game.ToggleFlag(row, col);
                break;
        }
    }

    Console.Clear();
    PrintBoard(game.Board);
    Console.WriteLine();
    Console.WriteLine(game.State == GameState.Won ? "You win!" : "BOOM! You hit a mine.");
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey(true);
}

static void PrintBoard(Board board)
{
    // Column headers
    Console.Write("    ");
    for (int c = 0; c < board.Columns; c++)
        Console.Write($"{c,3}");
    Console.WriteLine();

    Console.Write("    ");
    for (int c = 0; c < board.Columns; c++)
        Console.Write("---");
    Console.WriteLine();

    for (int r = 0; r < board.Rows; r++)
    {
        Console.Write($"{r,3}| ");
        for (int c = 0; c < board.Columns; c++)
        {
            var cell = board.GetCell(r, c);
            char display = cell.State switch
            {
                CellState.Flagged => 'F',
                CellState.Hidden => '.',
                CellState.Revealed when cell.IsMine => '*',
                CellState.Revealed when cell.AdjacentMineCount == 0 => ' ',
                CellState.Revealed => (char)('0' + cell.AdjacentMineCount),
                _ => '?'
            };
            Console.Write($" {display} ");
        }
        Console.WriteLine();
    }
}
