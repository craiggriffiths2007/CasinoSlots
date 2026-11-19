using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TempestTemple
{
    // WebGL displays complete rounds settled by the existing ASP.NET Tempest endpoints.
    // The local engine is used only by the editor and Windows practice build.
    public sealed class TempestCasinoBridge : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TempestCasinoInit(string target);
        [DllImport("__Internal")] static extern void TempestCasinoSpin(string target, int bet);
        [DllImport("__Internal")] static extern void TempestCasinoFinished();
        [DllImport("__Internal")] static extern void TempestCasinoBuyBonus(string target, int bet);
#endif
        Action<long, bool> loaded;
        Action<TempestRound, long> settled;
        Action<string> failed;
        public void Initialize(Action<long, bool> onLoaded, Action<TempestRound, long> onSettled, Action<string> onFailed)
        {
            loaded=onLoaded; settled=onSettled; failed=onFailed;
#if UNITY_WEBGL && !UNITY_EDITOR
            TempestCasinoInit(gameObject.name);
#endif
        }
        public void Spin(int bet)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TempestCasinoSpin(gameObject.name, bet);
#endif
        }
        public void BuyBonus(int bet)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TempestCasinoBuyBonus(gameObject.name, bet);
#endif
        }
        public void Finished()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TempestCasinoFinished();
#endif
        }
        public void TempestAccountLoaded(string json)
        {
            try
            {
                var account=JsonUtility.FromJson<Account>(json);
                if(account==null || account.balance<0) throw new Exception("Invalid account response.");
                loaded(account.balance, account.demo);
            }
            catch(Exception e) { failed(e.Message); }
        }
        public void TempestSpinCompleted(string json)
        {
            try
            {
                var response=JsonUtility.FromJson<Response>(json);
                if(response==null || response.balance<0 || response.result==null || response.result.turns==null || response.result.turns.Length==0)
                    throw new Exception("Incomplete spin response.");
                var round=response.result.ToRound();
                if(round.TotalWin<0 || round.Turns.Sum(t=>t.Win)!=round.TotalWin)
                    throw new Exception("Invalid round total.");
                settled(round,response.balance);
            }
            catch(Exception e) { failed(e.Message); }
        }
        public void TempestRequestFailed(string message) { failed(message); }

        [Serializable] sealed class Account { public long balance; public bool demo; }
        [Serializable] sealed class Response { public ServerRound result; public long balance; }
        [Serializable] sealed class ServerRound
        {
            public ServerTurn[] turns; public long totalWin; public bool capped;
            public TempestRound ToRound() => new TempestRound(turns.Select(t=>t.ToTurn()).ToList(),totalWin,capped);
        }
        [Serializable] sealed class ServerTurn
        {
            public bool freeSpin; public ServerFrame[] frames; public int multiplier, awarded, bank; public long win;
            public TempestTurn ToTurn()
            {
                if(frames==null || frames.Length==0 || frames.Length>20001 || multiplier<1 || awarded<0 || win<0)
                    throw new Exception("Invalid turn response.");
                return new TempestTurn(freeSpin,frames.Select(f=>f.ToFrame()).ToList(),multiplier,win,awarded,bank);
            }
        }
        [Serializable] sealed class ServerFrame
        {
            public int[] board, winning; public long win;
            public TempestFrame ToFrame()
            {
                if(board==null || board.Length!=30 || board.Any(s=>s<0 || s>10) || winning==null || winning.Any(i=>i<0 || i>=30) || win<0)
                    throw new Exception("Invalid board response.");
                return new TempestFrame(board,winning,win);
            }
        }
    }
}
