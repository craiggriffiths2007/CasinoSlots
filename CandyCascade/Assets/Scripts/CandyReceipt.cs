using System;

namespace CandyCascade
{
    // Public fields are shared by Unity JsonUtility and the server serializer.
    [Serializable] public sealed class CandyReceipt
    {
        public string requestId;
        public long balance, win, bet;
        public bool limitReached;
        public CandyRoundSpin[] spins;
    }
    [Serializable] public sealed class CandyRoundSpin
    {
        public int[] initialBoard, initialHits;
        public bool isFreeSpin;
        public int awarded, remaining;
        public CandyRoundCascade[] cascades;
    }
    [Serializable] public sealed class CandyRoundCascade
    {
        public int[] boardAfter;
        public long win;
    }
}
