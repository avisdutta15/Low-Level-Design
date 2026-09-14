using MineSweeper.Enums;

namespace MineSweeper.Entities;

/// <summary>
/// Maps difficulty presets to board dimensions and mine counts.
/// </summary>
public class DifficultyConfig
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public int MineCount { get; set; }

    public DifficultyConfig(int rows, int cols, int mineCount)
    {
        Rows = rows;
        Columns = cols;
        MineCount = mineCount;
    }

    public static DifficultyConfig GetConfig(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Easy     => new DifficultyConfig(9, 9, 10),
            Difficulty.Medium   => new DifficultyConfig(16, 16, 40),
            Difficulty.Hard     => new DifficultyConfig(30, 16, 99),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
        };
    }
}
