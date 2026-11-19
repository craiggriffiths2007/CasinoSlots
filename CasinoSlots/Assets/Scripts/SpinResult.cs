using System.Collections.Generic;

public class SpinResult
{
    public SymbolType[,] InitialBoard { get; set; }
        = new SymbolType[3, 5];

    public List<CascadeResult> Cascades { get; set; }
        = new();

    public long TotalWin { get; set; }
    public List<SpinResult> FreeSpins { get; set; } = new();
    public int FreeSpinsAwarded { get; set; }
}

public static class CascadeLadder
{
    public const int MaxBonusSpins = PhoenixSlot2Math.Engine.MaxBonusSpins;
    public static int Award(int wins) => PhoenixSlot2Math.Engine.Award(wins);
    public static int Retrigger(int wins) => PhoenixSlot2Math.Engine.Retrigger(wins);
}

public class CascadeResult
{
    public SymbolType[,] BoardBefore { get; set; }
        = new SymbolType[3, 5];

    public SymbolType[,] BoardAfter { get; set; }
        = new SymbolType[3, 5];

    public List<WinResult> Wins { get; set; }
        = new();

    public long Win { get; set; }

    public List<int> PhoenixReels { get; set; }
        = new();

    public List<int> ExpiredPhoenixReels { get; set; }
        = new();
}

public class WinResult
{
    public SymbolType Symbol { get; set; }

    public int ReelsMatched { get; set; }

    public int PaylineIndex { get; set; }

    public long Win { get; set; }

    public List<SymbolPosition> Positions { get; set; }
        = new();
}

public class SymbolPosition
{
    public int Row { get; set; }

    public int Reel { get; set; }
}
