using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Android Store")]
    public sealed class AndroidStoreConfig : ScriptableObject
    {
        [Tooltip("ID exato do Premium não consumível no Google Play e App Store Connect.")]
        public string removeAdsProductId = "primium";
    }
}
