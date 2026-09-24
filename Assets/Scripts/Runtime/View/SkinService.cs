using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Loads all FrogSkin ScriptableObjects from Resources/Skins, manages the
    // equipped selection, and persists it to PlayerPrefs so it survives restarts.
    public sealed class SkinService
    {
        private const string PrefKey = "SelectedSkin";

        private readonly FrogSkin[] _skins;
        private FrogSkin _equipped;

        public FrogSkin[]  Skins    => _skins;
        public FrogSkin    Equipped => _equipped;
        public Sprite      EquippedSprite => _equipped?.sprite;

        public SkinService()
        {
            _skins = Resources.LoadAll<FrogSkin>("Skins");

            // Restore the previously equipped skin; fall back to default.
            string savedId = PlayerPrefs.GetString(PrefKey, SkinCatalog.DefaultId);
            _equipped = FindById(savedId)
                     ?? FindById(SkinCatalog.DefaultId)
                     ?? (_skins.Length > 0 ? _skins[0] : null);
        }

        // Equip a skin and persist the selection.
        public void Equip(FrogSkin skin)
        {
            if (skin == null) return;
            _equipped = skin;
            PlayerPrefs.SetString(PrefKey, skin.id);
            PlayerPrefs.Save();
        }

        private FrogSkin FindById(string id)
        {
            foreach (var s in _skins)
                if (s != null && s.id == id) return s;
            return null;
        }
    }
}
