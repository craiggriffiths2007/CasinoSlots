using System;
using System.Collections.Generic;
using System.Linq;

namespace TempestTemple
{
    [Serializable] public sealed class TempestFrame
    {
        public int[] Board, Winning;
        public long Win;
        public TempestFrame(int[] board, int[] winning, long win) { Board = board; Winning = winning; Win = win; }
    }
    [Serializable] public sealed class TempestTurn
    {
        public bool FreeSpin;
        public List<TempestFrame> Frames;
        public int Multiplier, Awarded, Bank;
        public long Win;
        public TempestTurn(bool free, List<TempestFrame> frames, int multiplier, long win, int awarded, int bank = 0)
        { FreeSpin = free; Frames = frames; Multiplier = multiplier; Win = win; Awarded = awarded; Bank = bank; }
    }
    [Serializable] public sealed class TempestRound
    {
        public List<TempestTurn> Turns;
        public long TotalWin;
        public bool Capped;
        public TempestRound(List<TempestTurn> turns, long totalWin, bool capped) { Turns = turns; TotalWin = totalWin; Capped = capped; }
    }

    // Adapter around the same calibrated rules source used by the casino server.
    public sealed class TempestEngine
    {
        readonly TempestMath.Engine engine;
        public TempestEngine(int seed) { engine=new TempestMath.Engine(new Random(seed).Next); }
        public TempestEngine(Func<int,int> random) { engine=new TempestMath.Engine(random); }
        public static int PayUnits(int symbol,int count) => TempestMath.Engine.PayUnits(symbol,count);
        public static TempestFrame Evaluate(int[] board,long bet)
        { var f=TempestMath.Engine.Evaluate(board,bet); return new TempestFrame(f.Board,f.Winning,f.Win); }
        public static int[] SourceRows(int column,int[] removed)
        {
            var kept=Enumerable.Range(0,5).Where(r=>!removed.Contains(r*6+column)).ToList();
            int added=5-kept.Count;
            return Enumerable.Range(0,5).Select(r=>r<added?r-added:kept[r-added]).ToArray();
        }
        public TempestRound Play(long bet) => Convert(engine.Play(bet));
        public TempestRound BuyBonus(long bet) => Convert(engine.Play(bet,true));
        static TempestRound Convert(TempestMath.Round round) => new TempestRound(
            round.Turns.Select(t=>new TempestTurn(t.FreeSpin,
                t.Frames.Select(f=>new TempestFrame(f.Board,f.Winning,f.Win)).ToList(),
                t.Multiplier,t.Win,t.Awarded,t.Bank)).ToList(),round.TotalWin,round.Capped);
    }
}
