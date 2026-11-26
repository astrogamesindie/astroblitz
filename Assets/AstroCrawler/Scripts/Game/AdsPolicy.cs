namespace AstroCrawler
{
    /// <summary>Every ad adapter must check this both before loading and before showing ads.</summary>
    public static class AdsPolicy
    {
        public static bool CanShowAds
        {
            get
            {
                var store = AndroidPurchases.EnsureCreated();
                // Fail closed if the entitlement service cannot initialize.
                return store != null && !store.AdsRemoved &&
                    (StoreUpdateGate.Instance == null || !StoreUpdateGate.Instance.BlocksPlay);
            }
        }
    }
}
