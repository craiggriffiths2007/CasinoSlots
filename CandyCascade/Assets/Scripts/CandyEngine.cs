using System;
using System.Collections.Generic;

namespace CandyCascade
{
    // Pure game rules: no Unity dependency. Credits are fictional and kept in memory.
    public sealed class CandyEngine
    {
        public const int Size = 7, Scatter = 6, MaxMultiplier = 128;
        public readonly int[,] Board = new int[Size, Size];
        public readonly int[,] Hits = new int[Size, Size];
        readonly Random random;
        public sealed class Cluster
        {
            public int Symbol;
            public readonly List<int> Cells = new List<int>();
            public decimal Payout;
        }
        public readonly struct Fall
        {
            public readonly int Column, FromRow, ToRow, Symbol;
            public bool IsNew => FromRow < 0;
            public Fall(int column, int fromRow, int toRow, int symbol)
            { Column = column; FromRow = fromRow; ToRow = toRow; Symbol = symbol; }
        }
        public CandyEngine(int seed) { random = new Random(seed); }
        // Keep sprite/paytable IDs stable; blue (4) no longer enters the board.
        // Relative candy odds: pink 28%, mint 24%, violet 20%, orange 16%, yellow 12%.
        // Stars retain their separate 1.8% chance on every generated cell.
        static readonly int[] ActiveSymbols = { 0, 1, 2, 3, 5 };
        static readonly int[] CumulativeWeights = { 28, 52, 72, 88, 100 };
        int NextSymbol()
        {
            if (random.Next(1000) < 18) return Scatter;
            int roll = random.Next(CumulativeWeights[CumulativeWeights.Length - 1]);
            for (int i = 0; i < CumulativeWeights.Length; i++)
                if (roll < CumulativeWeights[i]) return ActiveSymbols[i];
            throw new InvalidOperationException("Invalid candy symbol weights.");
        }
        public void Fill(bool resetHits)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    Board[x, y] = NextSymbol();
                    if (resetHits) Hits[x, y] = 0;
                }
        }
        public int Multiplier(int x, int y) => Hits[x, y] < 2 ? 0 : 1 << Math.Min(7, Hits[x, y] - 1);
        public static decimal BasePay(int symbol, int count)
        {
            if (symbol < 0 || symbol >= Scatter || count < 5) return 0;
            decimal[] values = { .12m, .16m, .20m, .25m, .32m, .40m };
            int tier = count >= 15 ? 25 : count >= 12 ? 12 : count >= 10 ? 7 : count >= 8 ? 4 : count >= 6 ? 2 : 1;
            return values[symbol] * tier;
        }
        public List<Cluster> FindWins(decimal bet)
        {
            var result = new List<Cluster>();
            var visited = new bool[Size * Size];
            for (int start = 0; start < visited.Length; start++)
            {
                if (visited[start]) continue;
                int symbol = Board[start % Size, start / Size];
                visited[start] = true;
                if (symbol < 0 || symbol == Scatter) continue;
                var cluster = new Cluster { Symbol = symbol };
                var queue = new Queue<int>(); queue.Enqueue(start);
                int multiplier = 0;
                while (queue.Count > 0)
                {
                    int p = queue.Dequeue(), x = p % Size, y = p / Size;
                    cluster.Cells.Add(p); multiplier += Multiplier(x, y);
                    int[] neighbors = { x > 0 ? p - 1 : -1, x < Size - 1 ? p + 1 : -1,
                        y > 0 ? p - Size : -1, y < Size - 1 ? p + Size : -1 };
                    foreach (int n in neighbors)
                        if (n >= 0 && !visited[n] && Board[n % Size, n / Size] == symbol)
                        { visited[n] = true; queue.Enqueue(n); }
                }
                if (cluster.Cells.Count < 5) continue;
                cluster.Payout = bet * BasePay(symbol, cluster.Cells.Count) * Math.Max(1, multiplier);
                result.Add(cluster);
            }
            return result;
        }
        public void Remove(List<Cluster> wins)
        {
            foreach (var win in wins) foreach (int p in win.Cells)
            {
                int x = p % Size, y = p / Size;
                Board[x, y] = -1; Hits[x, y] = Math.Min(8, Math.Max(2, Hits[x, y] + 1));
            }
        }
        // Return the same movements used to update the board, so the view can move
        // each surviving candy rather than replacing it at its destination.
        public List<Fall> Collapse()
        {
            var falls = new List<Fall>(Size * Size);
            for (int x = 0; x < Size; x++)
            {
                int target = Size - 1;
                for (int y = Size - 1; y >= 0; y--)
                    if (Board[x, y] >= 0)
                    {
                        int symbol = Board[x, y];
                        Board[x, target] = symbol;
                        falls.Add(new Fall(x, y, target--, symbol));
                    }
                int missing = target + 1;
                while (target >= 0)
                {
                    int symbol = NextSymbol(); Board[x, target] = symbol;
                    falls.Add(new Fall(x, target - missing, target, symbol)); target--;
                }
            }
            return falls;
        }
        public int ScatterCount()
        {
            int count = 0; foreach (int s in Board) if (s == Scatter) count++; return count;
        }
        public static int FreeSpinAward(int scatters, bool inBonus) => scatters < 3 ? 0 : inBonus ? 5 : 10 + (scatters - 3) * 2;
    }
}
