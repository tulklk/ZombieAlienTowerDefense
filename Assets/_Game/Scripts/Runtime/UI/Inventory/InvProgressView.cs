using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>Artifacts tab overview: one panel per UFO part (Flight stick, Chair, Reactor, Cargo, Antigravity,
    /// Engine) with five rarity slots - a lock until the player owns an artifact of that part at that rarity.</summary>
    public sealed class InvProgressView : MonoBehaviour
    {
        [SerializeField, Tooltip("Indexed slot * 5 + rarity.")]
        private Image[] _icons = new Image[30];

        [SerializeField, Tooltip("Indexed slot * 5 + rarity.")]
        private Image[] _backgrounds = new Image[30];

        [SerializeField, Tooltip("Indexed slot * 5 + rarity.")]
        private GameObject[] _locks = new GameObject[30];

        public void Bind(bool[,] owned, Sprite[] iconPerSlot, Color[] rarityColors, Color lockedColor)
        {
            for (int slot = 0; slot < 6; slot++)
            {
                for (int r = 0; r < 5; r++)
                {
                    int i = slot * 5 + r;
                    bool has = owned != null && owned[slot, r];
                    if (i < _icons.Length && _icons[i] != null)
                    {
                        _icons[i].enabled = has;
                        _icons[i].sprite = iconPerSlot != null && slot < iconPerSlot.Length ? iconPerSlot[slot] : null;
                    }

                    if (i < _backgrounds.Length && _backgrounds[i] != null)
                    {
                        _backgrounds[i].color = has && rarityColors != null && r < rarityColors.Length ? rarityColors[r] : lockedColor;
                    }

                    if (i < _locks.Length && _locks[i] != null)
                    {
                        _locks[i].SetActive(!has);
                    }
                }
            }
        }
    }
}
