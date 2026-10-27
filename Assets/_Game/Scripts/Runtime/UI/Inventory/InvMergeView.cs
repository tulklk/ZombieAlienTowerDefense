using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>The "Artifacts merge" screen: rows of 5 source slots -> 1 result slot, the "no artifacts" message, Back
    /// and Merge (grey when nothing can merge). Rows are selectable; the presenter fills them from the merge plans.</summary>
    public sealed class InvMergeView : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public Button Button;
            public GameObject Highlight;
            public InvItemCardView[] Sources = Array.Empty<InvItemCardView>();
            public InvItemCardView Result;
        }

        [SerializeField]
        private Row[] _rows = Array.Empty<Row>();

        [SerializeField]
        private TMP_Text _message;

        [SerializeField]
        private Button _back;

        [SerializeField]
        private Button _merge;

        [SerializeField]
        private Image _mergeImage;

        [SerializeField]
        private Sprite _mergeOn;

        [SerializeField]
        private Sprite _mergeOff;

        [SerializeField]
        private Image _flash;

        private bool _wired;

        public Row[] Rows => _rows;
        public bool IsOpen => gameObject.activeSelf;

        public event Action BackClicked;
        public event Action MergeClicked;
        public event Action<int> RowClicked;

        public void Wire()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;
            _back?.onClick.AddListener(() => BackClicked?.Invoke());
            _merge?.onClick.AddListener(() => MergeClicked?.Invoke());
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = i;
                _rows[i].Button?.onClick.AddListener(() => RowClicked?.Invoke(index));
            }
        }

        public void SetMessage(string text)
        {
            if (_message == null) return;
            _message.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _message.text = text;
        }

        public void SetMergeEnabled(bool enabled)
        {
            if (_merge != null) _merge.interactable = enabled;
            if (_mergeImage != null) _mergeImage.sprite = enabled ? _mergeOn : _mergeOff;
        }

        public void PlayFlash()
        {
            if (_flash == null) return;
            Image flash = _flash;
            flash.gameObject.SetActive(true);
            flash.DOKill();
            flash.color = new Color(1f, 1f, 1f, 0.9f);
            DOTween.To(() => flash.color.a, a => { Color k = flash.color; k.a = a; flash.color = k; }, 0f, 0.5f)
                .SetUpdate(true).SetTarget(flash).SetLink(flash.gameObject).OnComplete(() => flash.gameObject.SetActive(false));
        }
    }
}
