using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Android Store")]
    public sealed class AndroidStoreConfig : ScriptableObject
    {
        [Tooltip("ID exato do Premium não consumível no App Store Connect.")]
        public string removeAdsProductId = "premium";

        [Tooltip("ID exato do Premium não consumível no Google Play Console.")]
        public string googlePlayProductId = "remove_ads";

        /// <summary>ID do produto na loja da plataforma atual.</summary>
        public string ProductIdForPlatform =>
            Application.platform == RuntimePlatform.Android && !string.IsNullOrWhiteSpace(googlePlayProductId)
                ? googlePlayProductId
                : removeAdsProductId;
    }
}
