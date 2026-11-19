#nullable disable // Shared Unity model: null frame lists are intentional in simulation-only mode.
using System;
using System.Collections.Generic;

// Shared verbatim by the casino and Unity. Probabilities are original Tempest calibration,
// not Pragmatic Play reel strips. Published Gates of Olympus rules/paytable are the reference.
namespace TempestMath
{
    public sealed class Frame
    {
        public int[] Board, Winning;
        public long Win;
        public Frame(int[] board, int[] winning, long win) { Board=board; Winning=winning; Win=win; }
    }
    public sealed class Turn
    {
        public bool FreeSpin;
        public List<Frame> Frames;
        public int Multiplier, Bank, Awarded;
        public long Win;
    }
    public sealed class Round
    {
        public List<Turn> Turns;
        public long TotalWin;
        public int TurnCount;
        public bool Capped, BonusTriggered;
    }
    public sealed class Profile
    {
        public readonly int BaseScatter, BaseLightning, FreeScatter, FreeLightning;
        public Profile(int baseScatter=1800, int baseLightning=600, int freeScatter=1500, int freeLightning=3900)
        { BaseScatter=baseScatter; BaseLightning=baseLightning; FreeScatter=freeScatter; FreeLightning=freeLightning; }
    }
    public sealed class Engine
    {
        public const string Version="tempest-gems-v4";
        // Payout units per 20-credit bet: sapphire, emerald, ruby, amethyst, star,
        // crystal, temple, crown, sun. Columns: 8–9 / 10–11 / 12+.
        static readonly int[,] Pays = {
            {2,5,12},{3,7,20},{4,10,35},{4,9,30},{5,10,50},
            {15,20,120},{20,50,150},{100,250,500},{25,100,250}
        };
        static readonly int[] Weights={175,165,145,145,110,80,70,45,65};
        static readonly int[] FreeWeights={185,165,125,130,120,85,75,50,65};
        static readonly int[] Values={2,3,4,5,6,8,10,12,15,20,25,50,100,250,500};
        static readonly int[] MultWeights={35000,20000,12000,10000,6000,5000,4000,2000,2000,1500,1200,800,350,100,50};
        readonly Func<int,int> next;
        readonly Profile profile;
        public Engine(Func<int,int> random, Profile settings=null) { next=random ?? throw new ArgumentNullException(nameof(random)); profile=settings ?? new Profile(); }
        public static void ValidateBet(long bet) { if(bet<20 || bet>1000 || bet%20!=0) throw new ArgumentOutOfRangeException(nameof(bet),"Choose 20–1,000 credits in steps of 20."); }
        public static int PayUnits(int symbol,int count) { return count<8 ? 0 : Pays[symbol,count>=12?2:count>=10?1:0]; }
        public static int ApplyMultiplier(bool free,long baseWin,int lightning,ref int bank)
        {
            if(baseWin<=0 || lightning<=0)return 1;
            if(!free)return lightning;
            bank=checked(bank+lightning);
            return bank;
        }
        int Draw(bool free,bool trigger,int focus=-1)
        {
            int scatter=trigger?0:free?profile.FreeScatter:profile.BaseScatter;
            int lightning=free?profile.FreeLightning:profile.BaseLightning;
            int roll=next(100000);
            if(roll<scatter) return 9;
            if(roll<scatter+lightning) return 10;
            // Only the opening board favours a randomly chosen low-value symbol.
            // Refill draws use ordinary weights, so the focus does not sustain cascades.
            if(focus>=0 && next(1000)<100)return focus;
            roll=next(1000);
            var weights=free?FreeWeights:Weights;
            for(int i=0;i<weights.Length;i++) { if(roll<weights[i])return i; roll-=weights[i]; }
            throw new InvalidOperationException("Invalid symbol weights.");
        }
        int Lightning()
        {
            int roll=next(100000);
            for(int i=0;i<Values.Length;i++) { if(roll<MultWeights[i])return Values[i]; roll-=MultWeights[i]; }
            throw new InvalidOperationException("Invalid multiplier weights.");
        }
        public static Frame Evaluate(int[] board,long bet)
        {
            ValidateBet(bet);
            if(board==null || board.Length!=30) throw new ArgumentException("Expected 30 symbols.");
            var counts=new int[11];
            foreach(int symbol in board) { if(symbol<0 || symbol>10)throw new ArgumentException("Unknown symbol."); counts[symbol]++; }
            var winning=new List<int>(); long win=0;
            for(int i=0;i<9;i++)win+=(bet/20)*PayUnits(i,counts[i]);
            for(int i=0;i<30;i++)if(board[i]<9 && counts[board[i]]>=8)winning.Add(i);
            return new Frame((int[])board.Clone(),winning.ToArray(),win);
        }
        public Round Play(long bet,bool buyBonus=false,bool capture=true)
        {
            ValidateBet(bet);
            var result=new Round {Turns=capture?new List<Turn>():null};
            long cap=bet*5000;
            int remaining=0,bank=0;
            int[] board=new int[30],counts=new int[11];
            do
            {
                // This is a malfunction guard, not a truncation or a settled partial bonus.
                if(result.TurnCount>=10000)throw new InvalidOperationException("Round exceeded safety bound.");
                bool free=result.TurnCount>0,trigger=buyBonus && !free;
                if(free)remaining--;
                int focus=next(4);
                for(int i=0;i<30;i++)board[i]=Draw(free,trigger,focus);
                if(trigger)
                {
                    int[] positions=new int[30];for(int i=0;i<30;i++)positions[i]=i;
                    for(int i=0;i<4;i++) { int pick=i+next(30-i); int temp=positions[i];positions[i]=positions[pick];positions[pick]=temp;board[positions[i]]=9; }
                }
                var frames=capture?new List<Frame>():null;
                long baseWin=0; int paidTumbles=0;
                while(true)
                {
                    Array.Clear(counts,0,counts.Length);for(int i=0;i<30;i++)counts[board[i]]++;
                    long win=0;for(int i=0;i<9;i++)win+=(bet/20)*PayUnits(i,counts[i]);
                    if(capture)
                    {
                        var winning=new List<int>();for(int i=0;i<30;i++)if(board[i]<9 && counts[board[i]]>=8)winning.Add(i);
                        frames.Add(new Frame((int[])board.Clone(),winning.ToArray(),win));
                    }
                    baseWin+=win; if(win>0)paidTumbles++;
                    // Every evaluated win is paid; the last allowed winning board ends the turn.
                    if(win==0 || baseWin>=cap-result.TotalWin || paidTumbles>=12)break;
                    for(int col=0;col<6;col++)
                    {
                        int destination=4;
                        for(int row=4;row>=0;row--) { int symbol=board[row*6+col]; if(symbol>=9 || counts[symbol]<8)board[(destination--)*6+col]=symbol; }
                        while(destination>=0)board[(destination--)*6+col]=Draw(free,trigger);
                    }
                }
                int lightning=0;
                for(int i=0;i<counts[10];i++)lightning+=Lightning();
                // The bank persists; it is applied only with a new multiplier on a winning spin.
                int multiplier=ApplyMultiplier(free,baseWin,lightning,ref bank);
                int scatters=counts[9];
                int awarded=free?(scatters>=3?5:0):(scatters>=4?15:0);
                long scatterWin=!free && scatters>=4?bet*(scatters>=6?100:scatters==5?5:3):0;
                long payout=Math.Min(cap-result.TotalWin,baseWin*multiplier+scatterWin);
                result.TotalWin+=payout;
                if(result.TotalWin==cap)awarded=0;
                remaining+=awarded;
                if(!free && awarded>0)result.BonusTriggered=true;
                if(capture)result.Turns.Add(new Turn {FreeSpin=free,Frames=frames,Multiplier=multiplier,Bank=bank,Win=payout,Awarded=awarded});
                result.TurnCount++;
            } while(remaining>0 && result.TotalWin<cap);
            result.Capped=result.TotalWin==cap;
            return result;
        }
    }
}






