using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Store Updates")]
    public sealed class StoreUpdateConfig : ScriptableObject
    {
        public bool enabledChecks = true;
        public bool checkDevelopmentBuilds;
        [Tooltip("Opcional: ID numérico da App Store. Vazio consulta o Bundle ID do app instalado.")]
        public string iosAppId = "";
        [Tooltip("País da App Store a consultar, em ISO de duas letras. Ajustar conforme distribuição.")]
        public string iosStoreCountry = "BR";
        [Range(5, 30)] public int timeoutSeconds = 15;
        [Min(60)] public int foregroundIntervalSeconds = 300;
    }
}
