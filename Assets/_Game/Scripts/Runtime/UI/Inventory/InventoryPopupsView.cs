using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>The inventory's popup layer. Holds the panels (equipment detail, craft, artifact detail, material /
    /// container, reward) and opens one at a time with a scale-in; the presenter fills the content. Display only.</summary>
    public sealed class InventoryPopupsView : MonoBehaviour
    {
        [Serializable]
        public sealed class EquipmentPanel
        {
            public RectTransform Root;
            public InvItemCardView Card;
            public TMP_Text Name;
            public TMP_Text Rarity;
            public TMP_Text Level;
            public TMP_Text Stats;
            public TMP_Text State;
            public Button EquipButton;
            public TMP_Text EquipLabel;
            public Button UpgradeButton;
            public Image UpgradeImage;
            public TMP_Text UpgradeLabel;
            public TMP_Text UpgradeCost;
            public Button RarityButton;
            public Image RarityImage;
            public TMP_Text RarityCost;
            public TMP_Text Gain;
        }

        [Serializable]
        public sealed class CraftPanel
        {
            public RectTransform Root;
            public Transform List;
            public InvCraftRowView RowTemplate;
            public TMP_Text Empty;
            public ScrollRect Scroll;
        }

        [Serializable]
        public sealed class ArtifactPanel
        {
            public RectTransform Root;
            public InvItemCardView Card;
            public TMP_Text Name;
            public TMP_Text Rarity;
            public TMP_Text Effect;
            public TMP_Text Progress;
            public TMP_Text Description;
        }

        [Serializable]
        public sealed class MaterialPanel
        {
            public RectTransform Root;
            public InvItemCardView Card;
            public TMP_Text Name;
            public TMP_Text Description;
            public TMP_Text Amount;
            public Button OpenOne;
            public Button OpenMax;
            public TMP_Text OpenMaxLabel;
        }

        [Serializable]
        public sealed class RewardPanel
        {
            public RectTransform Root;
            public TMP_Text Title;
            public Transform Grid;
            public InvItemCardView Template;
            public Image Flash;
            public RectTransform Glow;
        }

        [SerializeField]
        private Image _dim;

        [SerializeField]
        private Button[] _closeButtons = Array.Empty<Button>();

        public EquipmentPanel Equipment = new EquipmentPanel();
        public CraftPanel Craft = new CraftPanel();
        public ArtifactPanel Artifact = new ArtifactPanel();
        public MaterialPanel Material = new MaterialPanel();
        public RewardPanel Reward = new RewardPanel();

        private RectTransform _open;
        private bool _wired;

        public bool IsOpen => gameObject.activeSelf;
        public RectTransform OpenPanel => IsOpen ? _open : null;

        public event Action Closed;

        private void Wire()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;
            foreach (Button close in _closeButtons)
            {
                close?.onClick.AddListener(Close);
            }
        }

        public void Open(RectTransform panel)
        {
            Wire();
            gameObject.SetActive(true);
            _open = panel;
            foreach (RectTransform p in new[] { Equipment.Root, Craft.Root, Artifact.Root, Material.Root, Reward.Root })
            {
                if (p != null) p.gameObject.SetActive(p == panel);
            }

            if (_dim != null)
            {
                Image dim = _dim;
                dim.DOKill();
                Color c = dim.color;
                c.a = 0f;
                dim.color = c;
                DOTween.To(() => dim.color.a, a => { Color k = dim.color; k.a = a; dim.color = k; }, 0.72f, 0.15f)
                    .SetUpdate(true).SetTarget(dim).SetLink(gameObject);
            }

            if (panel != null)
            {
                panel.DOKill();
                panel.localScale = Vector3.one * 0.8f;
                panel.DOScale(1f, 0.24f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(panel.gameObject);
            }
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            Reward.Glow?.DOKill();
            gameObject.SetActive(false);
            _open = null;
            Closed?.Invoke();
        }

        /// <summary>Reward / craft success: white flash, rotating glow.</summary>
        public void PlayRewardFx()
        {
            if (Reward.Flash != null)
            {
                Image flash = Reward.Flash;
                flash.gameObject.SetActive(true);
                flash.DOKill();
                flash.color = new Color(1f, 1f, 1f, 0.85f);
                DOTween.To(() => flash.color.a, a => { Color k = flash.color; k.a = a; flash.color = k; }, 0f, 0.45f)
                    .SetUpdate(true).SetTarget(flash).SetLink(flash.gameObject)
                    .OnComplete(() => flash.gameObject.SetActive(false));
            }

            if (Reward.Glow != null)
            {
                Reward.Glow.DOKill();
                Reward.Glow.localRotation = Quaternion.identity;
                Reward.Glow.DORotate(new Vector3(0f, 0f, -360f), 10f, RotateMode.FastBeyond360).SetLoops(-1)
                    .SetEase(Ease.Linear).SetUpdate(true).SetLink(Reward.Glow.gameObject);
            }
        }

        /// <summary>Green "+X" floating up after an upgrade.</summary>
        public void PlayGain(string text)
        {
            TMP_Text gain = Equipment.Gain;
            if (gain == null)
            {
                return;
            }

            gain.text = text;
            gain.gameObject.SetActive(true);
            RectTransform rect = gain.rectTransform;
            rect.DOKill();
            gain.DOKill();
            Vector2 start = rect.anchoredPosition;
            gain.alpha = 1f;
            DOTween.Sequence().SetUpdate(true).SetLink(gain.gameObject)
                .Append(DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, start + new Vector2(0f, 70f), 0.8f))
                .Join(DOTween.To(() => gain.alpha, a => gain.alpha = a, 0f, 0.8f).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    rect.anchoredPosition = start;
                    gain.gameObject.SetActive(false);
                });
        }
    }
}
