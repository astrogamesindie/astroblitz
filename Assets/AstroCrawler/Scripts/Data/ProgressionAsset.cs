// AstroCrawler - regras de XP e nivel dentro da run
//
// One ScriptableObject per file, named after the class: Unity binds an asset to
// its MonoScript by file name, and a class sharing a file with another loses
// that binding ("No script asset for ...").

using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Progressao da run", fileName = "Progression")]
    public class ProgressionAsset : ScriptableObject
    {
        public ProgressionDefinition definition = new ProgressionDefinition();
    }
}
