// AstroCrawler - carta de melhoria
//
// One ScriptableObject per file, named after the class: Unity binds an asset to
// its MonoScript by file name, and a class sharing a file with another loses
// that binding ("No script asset for ...").

using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Carta", fileName = "Card_Nova")]
    public class CardAsset : ScriptableObject
    {
        public CardDefinition definition = new CardDefinition();
    }
}
