// AstroCrawler - onda da run
//
// One ScriptableObject per file, named after the class: Unity binds an asset to
// its MonoScript by file name, and a class sharing a file with another loses
// that binding ("No script asset for ...").

using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Onda", fileName = "Wave_Nova")]
    public class WaveAsset : ScriptableObject
    {
        public WaveDefinition definition = new WaveDefinition();
    }
}
