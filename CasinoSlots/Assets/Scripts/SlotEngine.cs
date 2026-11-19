using System;
using System.Linq;

// Phoenix Slot 2 uses the same maths source in Unity and the casino server.
public class SlotEngine
{
    private readonly PhoenixSlot2Math.Engine engine;
    public SlotEngine() : this(max => UnityEngine.Random.Range(0, max)) { }
    public SlotEngine(Func<int,int> random) { engine = new PhoenixSlot2Math.Engine(random); }
    public bool ForceWin { get => engine.ForceWin; set => engine.ForceWin = value; }
    public SpinResult Spin(int bet = 10, bool isFreeSpin = false) => Convert(engine.Spin(bet, isFreeSpin));
    private static SymbolType[,] Board(PhoenixSlot2Math.SymbolType[,] source)
    {
        var board = new SymbolType[3,5];
        for (int row=0;row<3;row++) for(int reel=0;reel<5;reel++)
            board[row,reel] = (SymbolType)(int)source[row,reel];
        return board;
    }
    private static SpinResult Convert(PhoenixSlot2Math.SpinResult result) => new SpinResult
    {
        InitialBoard = Board(result.InitialBoard), TotalWin = result.TotalWin,
        Cascades = result.Cascades.Select(c => new CascadeResult
        {
            BoardBefore = Board(c.BoardBefore), BoardAfter = Board(c.BoardAfter), Win = c.Win,
            PhoenixReels = c.PhoenixReels, ExpiredPhoenixReels = c.ExpiredPhoenixReels,
            Wins = c.Wins.Select(w => new WinResult
            {
                Symbol = (SymbolType)(int)w.Symbol, ReelsMatched = w.ReelsMatched,
                PaylineIndex = w.PaylineIndex, Win = w.Win,
                Positions = w.Positions.Select(p => new SymbolPosition {Row=p.Row,Reel=p.Reel}).ToList()
            }).ToList()
        }).ToList()
    };
}
