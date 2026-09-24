using UnityEngine;

namespace RainbowFroggy.View
{
    // ScriptableObject data for a single frog skin.
    // Assets live in Assets/Resources/Skins/ so SkinService can discover them
    // via Resources.LoadAll<FrogSkin>("Skins") without a scene reference.
    [CreateAssetMenu(fileName = "FrogSkin", menuName = "Rainbow Froggy/Frog Skin")]
    public sealed class FrogSkin : ScriptableObject
    {
        public string id;
        public string displayName;
        public int    unlockScore;
        public Sprite sprite;
    }
}
