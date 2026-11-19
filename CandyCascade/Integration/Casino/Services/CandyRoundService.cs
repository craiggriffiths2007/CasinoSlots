using CandyCascade;
using System.Security.Cryptography;

namespace Casino.Services;

public static class CandyRoundService
{
    public static bool ValidBet(long bet) => bet is 10 or 25 or 50 or 100;
    public static CandyReceipt Play(long bet, string requestId, int? testSeed = null)
    {
        if (!ValidBet(bet)) throw new ArgumentOutOfRangeException(nameof(bet));
        var engine = new CandyEngine(testSeed ?? RandomNumberGenerator.GetInt32(int.MaxValue));
        var spins = new List<CandyRoundSpin>();
        long win = 0, cap = checked(bet * 5000); int remaining = 0;
        bool limited = false;
        do
        {
            bool bonus = spins.Count > 0;
            if (bonus) remaining--;
            engine.Fill(!bonus);
            var spin = new CandyRoundSpin { initialBoard = Flatten(engine.Board), initialHits = Flatten(engine.Hits), isFreeSpin = bonus };
            var cascades = new List<CandyRoundCascade>();
            while (cascades.Count < 100 && win < cap)
            {
                var clusters = engine.FindWins(bet); if (clusters.Count == 0) break;
                long amount = Math.Min((long)decimal.Floor(clusters.Sum(c => c.Payout)), cap - win);
                win += amount; engine.Remove(clusters); engine.Collapse();
                cascades.Add(new CandyRoundCascade { boardAfter = Flatten(engine.Board), win = amount });
            }
            spin.awarded = CandyEngine.FreeSpinAward(engine.ScatterCount(), bonus);
            remaining += spin.awarded;
            // One receipt settles the paid spin and every awarded free spin. This
            // also means closing the browser cannot lose an already awarded bonus.
            limited = win >= cap || cascades.Count >= 100 || spins.Count >= 199;
            if (limited) remaining = 0;
            spin.remaining = remaining; spin.cascades = cascades.ToArray(); spins.Add(spin);
        } while (remaining > 0);
        return new CandyReceipt { requestId = requestId, bet = bet, win = win, limitReached = limited, spins = spins.ToArray() };
    }
    private static int[] Flatten(int[,] board)
    {
        var values = new int[49];
        for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) values[y * 7 + x] = board[x, y];
        return values;
    }
}
