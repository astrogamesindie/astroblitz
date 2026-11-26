using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Android Ads")]
    public sealed class AndroidAdsConfig : ScriptableObject
    {
        public string androidAppId = "ca-app-pub-6786791460039462~4341250352";
        public string bannerId = "ca-app-pub-6786791460039462/4966093004";
        public string rewardedId = "ca-app-pub-6786791460039462/6887522645";
        public string interstitialId = "ca-app-pub-6786791460039462/5519371163";
        public string appOpenId = "ca-app-pub-6786791460039462/1384685030";
        public string iosAppId = "ca-app-pub-6786791460039462~4024572074";
        public string iosBannerId = "ca-app-pub-6786791460039462/9480253740";
        public string iosInterstitialId = "ca-app-pub-6786791460039462/1009280511";
        public string iosRewardedId = "ca-app-pub-6786791460039462/1378227437";
        public string iosAppOpenId = "ca-app-pub-6786791460039462/9065145769";
        [Min(30)] public int interstitialIntervalSeconds = 90;
        [Min(30)] public int appOpenBackgroundSeconds = 30;
        [Tooltip("Use anúncios de teste também em builds release para QA. Development Builds sempre usam teste.")]
        public bool useTestAds;
        [Tooltip("Usa o cliente e os prefabs oficiais do Google Mobile Ads no Play Mode.")]
        public bool officialPreviewInEditor = true;
        [Min(0)] public int rewardCoins = 100;
    }
}
