// AstroCrawler - ships sold for real money (Google Play / App Store).
//
// Each paid ship is a one-time, non-consumable product "ship_<shipId>"
// (PlayConsole/produtos_naves/PRODUTOS_NAVES.md). The stores are the source
// of truth; PlayerPrefs keep an offline cache per ship. A ship bought with
// credits stays bought whatever the store says (ShipUnlocks key); a refund
// only removes the paid cache.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Scripting;

namespace AstroCrawler
{
    public sealed class ShipPurchases : MonoBehaviour
    {
        /// <summary>Ships with a store product. Vanguarda-7 is free; Noel Polar is an event ship.</summary>
        public static readonly string[] PaidShips = { "bastion", "spectre", "aurora", "orion" };

        public const string ProductPrefix = "ship_";
        private const string PaidPrefix = "AstroCrawler.Ships.v1.paid.";
        /// <summary>BusyShip while the Starter Pack is being bought (no ship has this id).</summary>
        public const string StarterBusy = "#starter";
        private const string StarterPaidKey = "AstroCrawler.Starter.v1.paid";

        public static ShipPurchases Instance { get; private set; }
        public event Action Changed;
        /// <summary>A store purchase just completed for this ship (not a restore at launch).</summary>
        public event Action<string> Purchased;
        /// <summary>The Starter Pack purchase just completed (not a restore at launch).</summary>
        public event Action StarterPurchased;

        public bool Busy { get; private set; }
        public string BusyShip { get; private set; }
        public string Status { get; private set; } = "connecting";
        public static bool Supported => Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer;

        private readonly Dictionary<string, string> prices = new Dictionary<string, string>();
        public string StarterPrice { get; private set; } = "";
        private float operationStarted;
        private bool purchaseInFlight;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AstroShipsConfigure(string receiver, string products);
        [DllImport("__Internal")] private static extern void AstroShipsRefresh([MarshalAs(UnmanagedType.I1)] bool restore);
        [DllImport("__Internal")] private static extern void AstroShipsBuy(string product);
        [DllImport("__Internal")] private static extern void AstroShipsDispose();
        private bool iosConfigured;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
#endif

        public static ShipPurchases EnsureCreated()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AstroCrawler.ShipPurchases");
            DontDestroyOnLoad(go);
            return go.AddComponent<ShipPurchases>();
        }

        public static bool IsPaidShip(string shipId) => Array.IndexOf(PaidShips, shipId) >= 0;

        /// <summary>The offline cache: true when a store confirmed the purchase on this device.</summary>
        public static bool OwnsPaid(string shipId) =>
            !string.IsNullOrEmpty(shipId) && PlayerPrefs.GetInt(PaidPrefix + shipId, 0) == 1;

        /// <summary>The store's formatted price ("R$ 9,90"), or empty while unknown.</summary>
        public string PriceOf(string shipId)
        {
            string price;
            return shipId != null && prices.TryGetValue(shipId, out price) ? price : "";
        }

        /// <summary>Store-confirmed Starter Pack (offline cache; a refund clears it).</summary>
        public static bool OwnsStarter => PlayerPrefs.GetInt(StarterPaidKey, 0) == 1;

#if UNITY_EDITOR
        /// <summary>Clears Editor test ownership; never revokes a purchase in a store.</summary>
        internal static void EditorClearLocalPurchases()
        {
            foreach (var shipId in PaidShips) PlayerPrefs.DeleteKey(PaidPrefix + shipId);
            PlayerPrefs.DeleteKey(StarterPaidKey);
        }
#endif

        public bool CanBuyStarter => Supported && !Busy && !string.IsNullOrEmpty(StarterPrice) && !OwnsStarter;

        public void BuyStarter()
        {
            if (!CanBuyStarter) return;
            purchaseInFlight = true;
            BusyShip = StarterBusy;
            Begin("buying");
            Call("purchase", StarterPack.ProductId);
        }

        public bool CanBuy(string shipId) =>
            Supported && !Busy && ShipUnlocks.IsAvailableForAcquisition(shipId) && IsPaidShip(shipId) &&
            !string.IsNullOrEmpty(PriceOf(shipId)) && !OwnsPaid(shipId);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Supported) Refresh(false);
            else Status = "unsupported";
        }

        public void Buy(string shipId)
        {
            if (!CanBuy(shipId)) return;
            purchaseInFlight = true;
            BusyShip = shipId;
            Begin("buying");
            Call("purchase", ProductPrefix + shipId);
        }

        /// <summary>"Restaurar compras" (required by Apple for non-consumables).</summary>
        public void Restore()
        {
            Refresh(true);
        }

        /// <summary>Refreshes store-localized prices when the player opens the hangar.</summary>
        public void RefreshProducts()
        {
            Refresh(false);
        }

        private void Refresh(bool restore)
        {
            if (!Supported || Busy) return;
            Begin("restoring");
            Call(restore ? "restore" : "refresh", null);
        }

        private void Begin(string status)
        {
            Busy = true;
            Status = status;
            operationStarted = Time.realtimeSinceStartup;
            Changed?.Invoke();
        }

        private static string ProductList()
        {
            var ids = new string[PaidShips.Length + 1];
            for (var i = 0; i < PaidShips.Length; i++) ids[i] = ProductPrefix + PaidShips[i];
            ids[PaidShips.Length] = StarterPack.ProductId;
            return string.Join(",", ids);
        }

        private void Call(string method, string product)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (bridge == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        bridge = new AndroidJavaObject("com.astrocrawler.store.AstroShipBilling", activity,
                            gameObject.name, ProductList());
                }
                if (method == "purchase") bridge.Call("purchase", product);
                else bridge.Call("refresh");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] Loja de naves indisponível: " + exception.GetType().Name);
                Fail();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!iosConfigured)
                {
                    AstroShipsConfigure(gameObject.name, ProductList());
                    iosConfigured = true;
                }
                if (method == "purchase") AstroShipsBuy(product);
                else AstroShipsRefresh(method == "restore");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] StoreKit (naves) indisponível: " + exception.GetType().Name);
                Fail();
            }
#endif
        }

        private void Fail()
        {
            Busy = false;
            BusyShip = null;
            purchaseInFlight = false;
            Status = "error";
            Changed?.Invoke();
        }

        [Serializable]
        private sealed class StoreMessage
        {
            public string type;
            public string product;
            public string price;
            public bool owned;
        }

        private static string ShipOf(string product) =>
            !string.IsNullOrEmpty(product) && product.StartsWith(ProductPrefix) ? product.Substring(ProductPrefix.Length) : null;

        // Invoked by UnitySendMessage on the Unity thread; preserved for IL2CPP stripping.
        [Preserve]
        public void OnShipStoreEvent(string json)
        {
            var message = JsonUtility.FromJson<StoreMessage>(json);
            if (message == null) return;
            var ship = ShipOf(message.product);
            switch (message.type)
            {
                case "product" when message.product == StarterPack.ProductId:
                    if (!string.IsNullOrEmpty(message.price)) StarterPrice = message.price;
                    break;
                case "owned" when message.product == StarterPack.ProductId:
                    PlayerPrefs.SetInt(StarterPaidKey, message.owned ? 1 : 0);
                    PlayerPrefs.Save();
                    if (message.owned && purchaseInFlight && BusyShip == StarterBusy)
                    {
                        purchaseInFlight = false; Busy = false; BusyShip = null; Status = "ready";
                        StarterPurchased?.Invoke();
                    }
                    else if (!purchaseInFlight) { Busy = false; Status = "ready"; }
                    break;
                case "product":
                    if (ship != null && !string.IsNullOrEmpty(message.price)) prices[ship] = message.price;
                    break;
                case "products":
                    if (!purchaseInFlight) { Busy = false; Status = "ready"; }
                    break;
                case "owned":
                    if (ship == null || !IsPaidShip(ship)) break;
                    var was = OwnsPaid(ship);
                    PlayerPrefs.SetInt(PaidPrefix + ship, message.owned ? 1 : 0);
                    PlayerPrefs.Save();
                    if (message.owned && !was) ProgressSnapshot.SaveLocal();
                    if (message.owned && purchaseInFlight && ship == BusyShip)
                    {
                        purchaseInFlight = false;
                        Busy = false;
                        BusyShip = null;
                        Status = "ready";
                        Purchased?.Invoke(ship);
                    }
                    else if (!purchaseInFlight)
                    {
                        Busy = false;
                        Status = "ready";
                    }
                    break;
                case "pending":
                    purchaseInFlight = false; Busy = false; BusyShip = null; Status = "pending";
                    break;
                case "cancelled":
                    purchaseInFlight = false; Busy = false; BusyShip = null; Status = "cancelled";
                    break;
                case "error":
                    purchaseInFlight = false; Busy = false; BusyShip = null; Status = "error";
                    break;
            }
            Changed?.Invoke();
        }

        private void Update()
        {
            // A lost store callback can never lock the buttons for good.
            if (Busy && Time.realtimeSinceStartup - operationStarted > 60f) Fail();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && Supported && !Busy) Refresh(false);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) { bridge.Call("dispose"); bridge.Dispose(); bridge = null; }
#endif
#if UNITY_IOS && !UNITY_EDITOR
            if (iosConfigured) AstroShipsDispose();
#endif
            Changed = null;
            Purchased = null;
            StarterPurchased = null;
            Instance = null;
        }
    }
}
