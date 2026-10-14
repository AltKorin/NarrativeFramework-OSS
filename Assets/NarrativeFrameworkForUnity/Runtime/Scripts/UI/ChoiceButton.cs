using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimulationGame.UI
{
    /// <summary>
    /// Choice button with optional hover preview.
    /// - Call Setup(choice, onClick)
    /// - Call SetPreview(items) to provide precomputed outcome previews
    /// - On hover, shows _previewPanel and writes lines into _previewLabel
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ChoiceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI")]
        [SerializeField] private TMP_Text _label;                 // The button text
        [SerializeField] private Button _button;                  // Auto-wired

        [Header("Preview (optional)")]
        [SerializeField] private GameObject _previewPanel;        // Container panel (Image + RectTransform)
        [SerializeField] private TMP_Text _previewLabel;          // TMP to show preview lines
        [SerializeField] private bool _showPreviewOnHover = true; // If false, panel shows immediately (debug)
        [SerializeField, Min(0f)] private float _hoverShowDelay = 0.05f;
        [SerializeField, Min(0f)] private float _hoverHideDelay = 0.05f;

        private List<GameController.OutcomePreviewItem> _previewItems;
        private Choice _choice;
        private Action<Choice> _onClick;

        private float _hoverTimer;
        private bool _pendingShow;
        private bool _pendingHide;

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_label == null) _label = FindUsableLabel();

            // make sure we only have our handler
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnButtonClick);

            // ensure preview starts hidden in play mode
            if (_previewPanel != null) _previewPanel.SetActive(false);
        }

        private void Update()
        {
            // simple delayed show/hide
            if (_pendingShow)
            {
                _hoverTimer += Time.unscaledDeltaTime;
                if (_hoverTimer >= _hoverShowDelay)
                {
                    _pendingShow = false;
                    _hoverTimer = 0f;
                    ReallyShowPreview();
                }
            }
            if (_pendingHide)
            {
                _hoverTimer += Time.unscaledDeltaTime;
                if (_hoverTimer >= _hoverHideDelay)
                {
                    _pendingHide = false;
                    _hoverTimer = 0f;
                    ReallyHidePreview();
                }
            }
        }

        public void Setup(Choice choice, Action<Choice> onClick)
        {
            _choice  = choice;
            _onClick = onClick;

            var incoming = choice != null ? (choice.ChoiceText ?? "(null)") : "(choice is null)";
            Debug.Log($"[ChoiceButton] Setup for '{choice?.name ?? "NULL"}' text='{incoming}'", this);

            EnsureLabel();

            string caption = choice?.ChoiceText;
            if (string.IsNullOrWhiteSpace(caption))
                caption = choice?.name ?? "Choice";

            _label.richText = true;
            _label.textWrappingMode = TextWrappingModes.Normal;
            _label.text = caption;
            _label.ForceMeshUpdate();

            // make sure visible
            _label.enabled = true;
            _label.gameObject.SetActive(true);

            // rebuild layout so text appears
            Canvas.ForceUpdateCanvases();
            if (TryGetComponent<RectTransform>(out var rt))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Debug.Log($"[ChoiceButton] Label bound: '{_label.gameObject.name}', len={_label.text?.Length}", this);
        }

        public void SetPreview(List<GameController.OutcomePreviewItem> preview)
        {
            _previewItems = preview;
            Debug.Log($"[ChoiceButton] SetPreview: items={(preview == null ? 0 : preview.Count)} on '{_choice?.name ?? "NULL"}'", this);
            RefreshPreviewTextOnly(); // do not show yet — hover will do it
        }

        private void OnButtonClick()
        {
            if (_choice == null)
            {
                Debug.LogWarning("[ChoiceButton] Click with null _choice", this);
                return;
            }
            _onClick?.Invoke(_choice);
        }

        // ----- Hover -----
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_showPreviewOnHover)
            {
                ReallyShowPreview();
                return;
            }

            _pendingHide = false;
            _pendingShow = true;
            _hoverTimer = 0f;

            Debug.Log($"[ChoiceButton] PointerEnter on '{_choice?.name ?? "NULL"}'", this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {

            if (!_showPreviewOnHover)
            {
                ReallyHidePreview();
                return;
            }

            _pendingShow = false;
            _pendingHide = true;
            _hoverTimer = 0f;

            Debug.Log($"[ChoiceButton] PointerExit on '{_choice?.name ?? "NULL"}'", this);
        }

        private void ReallyShowPreview()
        {
            if (_previewPanel == null || _previewLabel == null)
            {
                Debug.Log($"[ChoiceButton] Preview not wired on '{_choice?.name ?? "NULL"}' (panel/label missing)", this);
                return;
            }

            if (_previewItems == null || _previewItems.Count == 0)
            {
                _previewPanel.SetActive(false);
                Debug.Log($"[ChoiceButton] No preview items for '{_choice?.name ?? "NULL"}' — hiding", this);
                return;
            }

            RefreshPreviewTextOnly();
            _previewPanel.SetActive(true);

            // non-blocking pointer
            var cg = _previewPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            Debug.Log($"[ChoiceButton] ShowPreview: {_previewItems.Count} items for '{_choice?.name ?? "NULL"}'", this);
        }

        private void ReallyHidePreview()
        {
            if (_previewPanel == null) return;
            _previewPanel.SetActive(false);
            Debug.Log($"[ChoiceButton] HidePreview for '{_choice?.name ?? "NULL"}'", this);
        }

        private void RefreshPreviewTextOnly()
        {
            if (_previewLabel == null) return;

            if (_previewItems == null || _previewItems.Count == 0)
            {
                _previewLabel.text = string.Empty;
                return;
            }

            var sb = new StringBuilder(128);
            foreach (var item in _previewItems)
            {
                // item.label is already formatted string from GameController
                sb.AppendLine(item.label);
            }
            _previewLabel.text = sb.ToString();
        }

        // ----- Utilities -----
        private void EnsureLabel()
        {
            if (_label != null) return;
            _label = FindUsableLabel();
            if (_label == null)
            {
                Debug.LogWarning("[ChoiceButton] No TMP_Text found; assign _label in prefab.", this);
            }
        }

        private TMP_Text FindUsableLabel()
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            TMP_Text best = null;

            foreach (var t in texts)
            {
                string n = t.gameObject.name.ToLowerInvariant();
                if (n.Contains("label") || n.Contains("text") || n.Contains("caption") || n.Contains("title"))
                {
                    best = t;
                    break;
                }
            }
            if (best == null && texts.Length > 0) best = texts[0];
            return best;
        }
    }
}
