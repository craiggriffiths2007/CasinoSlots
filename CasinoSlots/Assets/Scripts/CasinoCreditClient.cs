using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

public class CasinoCreditClient
{
    private const string PendingKey = "PhoenixSlot2.PendingSpin.v1";
    private string token;
    private string userId;
    private PendingSpin pending;
    private long? settledBalance;
    public bool Ready { get; private set; }
    public bool HasPending => pending != null;
    public long Balance { get; private set; }
    public string Error { get; private set; }
    public SpinResult Result { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PhoenixBalanceUpdated(string balance);
#endif

    private static string Endpoint(string action)
    {
        // Requests stay on the hosting casino origin and use its existing login cookie.
        Uri page = new Uri(Application.absoluteURL);
        return new Uri(page, "../../Game/PhoenixSlot2/" + action).AbsoluteUri;
    }

    public IEnumerator Connect()
    {
        Ready = false;
        Error = null;
        using var request = UnityWebRequest.Get(Endpoint("account"));
        request.timeout = 20;
        request.SetRequestHeader("X-Requested-With", "XMLHttpRequest");
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            Error = request.responseCode == 401 || request.responseCode == 403
                ? "Sign in to the casino, then reload this page."
                : "Cannot load credits. Click RETRY to reconnect.";
            yield break;
        }
        try
        {
            var account = JsonUtility.FromJson<AccountReply>(request.downloadHandler.text);
            if (account == null || string.IsNullOrEmpty(account.userId) || string.IsNullOrEmpty(account.token))
                throw new FormatException("Invalid account response.");
            userId = account.userId;
            token = account.token;
            SetBalance(account.balance);
            pending = null;
            string saved = PlayerPrefs.GetString(PendingKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                var stored = JsonUtility.FromJson<PendingSpin>(saved);
                if (stored != null && stored.accountId == userId && Guid.TryParse(stored.requestId, out _)
                    && new[] { 10, 25, 50, 100 }.Contains(stored.bet))
                    pending = stored;
                else
                    ClearPending();
            }
            Ready = true;
            if (HasPending) Error = "Previous spin pending. Click RETRY to recover it.";
        }
        catch (Exception)
        {
            Error = "Cannot read account details. Sign in and reload this page.";
        }
    }

    public IEnumerator Spin(int bet)
    {
        Error = null;
        Result = null;
        if (!Ready)
        {
            Error = "Connect to the casino first.";
            yield break;
        }
        if (pending == null)
        {
            pending = new PendingSpin { accountId = userId, requestId = Guid.NewGuid().ToString("D"), bet = bet };
            PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(pending));
            PlayerPrefs.Save();
        }

        var form = new WWWForm();
        form.AddField("bet", pending.bet);
        form.AddField("requestId", pending.requestId);
        form.AddField("accountId", pending.accountId);
        form.AddField("__RequestVerificationToken", token);
        using var request = UnityWebRequest.Post(Endpoint("spin"), form);
        request.timeout = 30;
        request.SetRequestHeader("X-Requested-With", "XMLHttpRequest");
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            if (request.responseCode == 400)
            {
                // Validation failures cannot have settled a spin on this endpoint.
                Error = "Spin rejected. Reload the page to refresh your login.";
                try
                {
                    var failure = JsonUtility.FromJson<FailureReply>(request.downloadHandler.text);
                    if (!string.IsNullOrEmpty(failure?.message))
                    {
                        Error = failure.message;
                        ClearPending();
                    }
                    else
                        Ready = false;
                    if (request.downloadHandler.text.Contains("\"balance\"")) SetBalance(failure.balance);
                }
                catch (Exception) { Ready = false; }
            }
            else if (request.responseCode == 401 || request.responseCode == 403 || request.responseCode == 409)
            {
                Ready = false;
                Error = "Your session changed. Sign in and reload the game.";
            }
            else
                Error = "Connection interrupted. Click RETRY to recover this spin.";
            yield break;
        }
        try
        {
            var response = JsonUtility.FromJson<SpinReply>(request.downloadHandler.text);
            if (response == null || response.requestId != pending.requestId)
                throw new FormatException("Spin request mismatch.");
            Result = response.ToResult();
            // The server has settled the spin, but reveal the winnings only after playback.
            // Derive this from the receipt so retrying cannot deduct the bet twice.
            settledBalance = response.balance;
            SetBalance(Math.Max(0L, response.balance - response.win));
            ClearPending();
        }
        catch (Exception)
        {
            Error = "Cannot read the spin result. Click RETRY to recover it.";
        }
    }

    public void FinishSpinPresentation()
    {
        if (!settledBalance.HasValue) return;
        SetBalance(settledBalance.Value);
        settledBalance = null;
    }

    private void ClearPending()
    {
        pending = null;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
    }

    private void SetBalance(long value)
    {
        if (value < 0) throw new FormatException("Invalid balance.");
        Balance = value;
#if UNITY_WEBGL && !UNITY_EDITOR
        PhoenixBalanceUpdated(value.ToString(CultureInfo.InvariantCulture));
#endif
    }

    [Serializable] private class PendingSpin { public string accountId; public string requestId; public int bet; }
    [Serializable] private class AccountReply { public string userId = ""; public string token = ""; public long balance = 0; }
    [Serializable] private class FailureReply { public string message = ""; public long balance = 0; }

    [Serializable] public class SpinReply
    {
        public string requestId;
        public string[] initialBoard;
        public CascadeReply[] cascades;
        public long win;
        public long balance;
        public SpinReply[] freeSpins;
        public int freeSpinsAwarded;

        public SpinResult ToResult()
        {
            if (cascades == null || win < 0 || balance < 0) throw new FormatException("Invalid spin.");
            return new SpinResult
            {
                InitialBoard = Board(initialBoard), TotalWin = cascades.Sum(c => c.win),
                FreeSpinsAwarded = freeSpinsAwarded,
                Cascades = cascades.Select(c => c.ToResult()).ToList(),
                FreeSpins = freeSpins == null ? new List<SpinResult>() : freeSpins.Select(s => s.ToResult()).ToList()
            };
        }
    }

    public static SymbolType[,] Board(string[] cells)
    {
        if (cells == null || cells.Length != 15) throw new FormatException("Expected a 3 by 5 board.");
        var board = new SymbolType[3, 5];
        for (int i = 0; i < cells.Length; i++)
        {
            if (!Enum.TryParse(cells[i], out SymbolType symbol) || !Enum.IsDefined(typeof(SymbolType), symbol))
                throw new FormatException("Unknown symbol.");
            board[i / 5, i % 5] = symbol;
        }
        return board;
    }

    [Serializable] public class CascadeReply
    {
        public string[] boardBefore;
        public string[] boardAfter;
        public WinReply[] wins;
        public long win;
        public int[] phoenixReels;
        public int[] expiredPhoenixReels;
        public CascadeResult ToResult()
        {
            if (wins == null || phoenixReels == null || expiredPhoenixReels == null || win < 0
                || phoenixReels.Concat(expiredPhoenixReels).Any(r => r < 0 || r >= 5))
                throw new FormatException("Invalid cascade.");
            return new CascadeResult
            {
                BoardBefore = Board(boardBefore), BoardAfter = Board(boardAfter), Win = win,
                Wins = wins.Select(w => w.ToResult()).ToList(),
                PhoenixReels = phoenixReels.ToList(), ExpiredPhoenixReels = expiredPhoenixReels.ToList()
            };
        }
    }

    [Serializable] public class WinReply
    {
        public string symbol;
        public int reelsMatched;
        public int paylineIndex;
        public long win;
        public PositionReply[] positions;
        public WinResult ToResult()
        {
            if (positions == null || positions.Any(p => p == null || p.row < 0 || p.row >= 3 || p.reel < 0 || p.reel >= 5)
                || !Enum.TryParse(symbol, out SymbolType type) || !Enum.IsDefined(typeof(SymbolType), type) || win < 0)
                throw new FormatException("Invalid winning positions.");
            return new WinResult
            {
                Symbol = type, ReelsMatched = reelsMatched, PaylineIndex = paylineIndex, Win = win,
                Positions = positions.Select(p => new SymbolPosition { Row = p.row, Reel = p.reel }).ToList()
            };
        }
    }
    [Serializable] public class PositionReply { public int row; public int reel; }
}
