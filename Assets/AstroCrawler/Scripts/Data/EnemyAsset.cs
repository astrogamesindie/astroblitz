// AstroCrawler - arquetipo de inimigo
//
// One ScriptableObject per file, named after the class: Unity binds an asset to
// its MonoScript by file name, and a class sharing a file with another loses
// that binding ("No script asset for ...").

using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Inimigo", fileName = "Enemy_Novo")]
    public class EnemyAsset : ScriptableObject
    {
        public EnemyDefinition definition = new EnemyDefinition();
    }
}
