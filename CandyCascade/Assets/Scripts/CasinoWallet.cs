using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CandyCascade
{
    public sealed class CasinoWallet : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CandyLoadCasinoAccount(
            string gameObjectName);

        [DllImport("__Internal")]
        private static extern void CandyCasinoSpin(
            string gameObjectName,
            long bet,
            string requestId,
            string accountId,
            string token);
        [DllImport("__Internal")]
        private static extern void CandyCasinoBuyFreeSpins(string gameObjectName, long bet, string requestId, string accountId, string token);
#endif

        public decimal Balance { get; private set; }

        public string AccountId { get; private set; } = "";

        public string RequestToken { get; private set; } = "";

        public bool IsLoaded { get; private set; }

        public bool SpinPending { get; private set; }

        private Action<decimal> _onLoaded;
        string pendingPurchaseId;
        decimal pendingPurchaseBet;

        public string BuyFreeSpins(decimal candyBet)
        {
            if (!IsLoaded || SpinPending) return "";
            if (pendingPurchaseId != null && pendingPurchaseBet != candyBet) return "";
            if (pendingPurchaseId == null)
            {
                pendingPurchaseId = Guid.NewGuid().ToString("D");
                pendingPurchaseBet = candyBet;
            }
            SpinPending = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            CandyCasinoBuyFreeSpins(gameObject.name, decimal.ToInt64(candyBet * 100m), pendingPurchaseId, AccountId, RequestToken);
#endif
            return pendingPurchaseId;
        }

        public IEnumerator LoadAccount(
            Action<decimal> onLoaded)
        {
            _onLoaded = onLoaded;

#if UNITY_WEBGL && !UNITY_EDITOR
            CandyLoadCasinoAccount(gameObject.name);
#else
            // Editor test balance.
            Balance = 1000m;
            IsLoaded = true;

            _onLoaded?.Invoke(Balance);
#endif

            yield return null;
        }

        public void CasinoAccountLoaded(string data)
        {
            string[] parts =
                data.Split('|');

            if (parts.Length != 3)
            {
                Debug.LogError(
                    $"Invalid casino account data: {data}");

                return;
            }

            if (!long.TryParse(
                parts[0],
                out var credits))
            {
                Debug.LogError(
                    $"Invalid casino balance: {parts[0]}");

                return;
            }

            Balance = credits / 100m;

            AccountId = parts[1];
            RequestToken = parts[2];

            IsLoaded = true;

            Debug.Log(
                $"Casino balance loaded: {Balance:N2}");

            _onLoaded?.Invoke(Balance);
        }

        public string BeginSpin(decimal candyBet)
        {
            if (!IsLoaded || SpinPending || pendingPurchaseId != null)
                return "";

            long casinoBet =
                decimal.ToInt64(candyBet * 100m);

            string requestId =
                Guid.NewGuid().ToString("D");

            SpinPending = true;

#if UNITY_WEBGL && !UNITY_EDITOR
            CandyCasinoSpin(
                gameObject.name,
                casinoBet,
                requestId,
                AccountId,
                RequestToken);
#else
            Debug.Log(
                $"Candy spin: {casinoBet} credits");
#endif

            return requestId;
        }

        public void CasinoSpinLoaded(string json)
        {
            SpinPending = false;
            pendingPurchaseId = null;

            // We'll deserialize and animate this in CandyGame next.
            Debug.Log(
                $"Candy server spin: {json}");

            SendMessage(
                "OnCasinoSpinLoaded",
                json,
                SendMessageOptions.DontRequireReceiver);
        }

        public void CasinoSpinFailed(string error)
        {
            SpinPending = false;

            Debug.LogError(
                $"Candy spin failed: {error}");

            SendMessage(
                "OnCasinoSpinFailed",
                error,
                SendMessageOptions.DontRequireReceiver);
        }

        public void CasinoPurchaseRejected(string error)
        {
            // A definite rejection charged nothing; allow a different bet.
            // Network/5xx failures keep the request ID for a safe retry.
            pendingPurchaseId = null;
            CasinoSpinFailed(error);
        }

        public void CasinoAccountFailed(string error)
        {
            Debug.LogError(
                $"Failed to load Casino account: {error}");
        }
    }
}
