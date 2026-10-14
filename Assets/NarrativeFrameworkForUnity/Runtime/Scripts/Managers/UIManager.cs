using SimulationGame.Performance;
using System;
using System.Collections.Generic;
using SimulationGame.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SimulationGame.DebugTools;

namespace SimulationGame.UI
{
    public class UIManager : MonoBehaviour, IUIManager
    {
        [Header("UI Elements")]
        [SerializeField] private Image _mainIllustrationImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private AudioSource _ambientAudioSource;

        [SerializeField] private TextMeshProUGUI _dayText;
        [SerializeField] private TextMeshProUGUI _eventTitleText;
        [SerializeField] private TextMeshProUGUI _eventDescriptionText;

        [Header("Choices")]
        [SerializeField] private Transform _choiceContainer;
        [SerializeField] private Button _choiceButtonPrefab;
        private readonly List<ChoiceButton> _choiceButtons = new List<ChoiceButton>();

        [SerializeField] private Button _nextDayButton;

        [Header("Resources HUD")]
        [SerializeField] private List<ResourceDisplay> _resourceDisplays = new List<ResourceDisplay>();
        private readonly Dictionary<string, ResourceDisplay> _resourceDisplayMap = new();

        [SerializeField] private Transform _resourceContainer;
        [SerializeField] private GameObject _resourceDisplayPrefab;

        [Header("Pooling (optional)")]
        [SerializeField] private ObjectPooler _objectPooler;
        [SerializeField] private bool _usePooling = false;
        [SerializeField] private string _choicePoolTag = "ChoiceButton";

        public event Action<Choice> OnChoiceSelected;
        public event Action OnNextDayClicked;

        private void Awake()
        {
            if (_objectPooler == null)
                _objectPooler = FindFirstObjectByType<ObjectPooler>();

            if (_nextDayButton != null)
                _nextDayButton.onClick.AddListener(() => OnNextDayClicked?.Invoke());
        }

        public void SetDayText(string dayInfo)
        {
            if (_dayText != null)
                _dayText.text = dayInfo;
        }

        public void DisplayEvent(SimEventSO evt)
        {
            Log.Info($"[UIManager] Displaying event: {evt?.name ?? "(null)"}");

            if (_eventTitleText != null)
                _eventTitleText.text = string.IsNullOrWhiteSpace(evt?.simEventTitle)
                                      ? evt?.Title
                                      : evt?.simEventTitle;

            if (_eventDescriptionText != null)
                _eventDescriptionText.text = string.IsNullOrWhiteSpace(evt?.simEventDescription)
                                           ? evt?.Description
                                           : evt?.simEventDescription;

            if (_mainIllustrationImage != null)
                _mainIllustrationImage.sprite = evt != null ? evt.mainIllustration : null;

            if (_backgroundImage != null)
                _backgroundImage.sprite = evt != null ? evt.backgroundImage : null;

            if (_ambientAudioSource != null)
            {
                _ambientAudioSource.Stop();
                _ambientAudioSource.clip = evt != null ? evt.ambientSound : null;
                if (evt != null && evt.ambientSound != null)
                    _ambientAudioSource.Play();
            }
        }

        private void ClearChoices()
        {
            foreach (var button in _choiceButtons)
            {
                if (_usePooling && _objectPooler != null)
                    _objectPooler.ReturnToPool(_choicePoolTag, button.gameObject);
                else
                    Destroy(button.gameObject);
            }
            _choiceButtons.Clear();
        }

        public void DisplayChoices(IList<Choice> choices)
        {
            ClearChoices();
            if (choices == null || _choiceContainer == null)
            {
                Log.Warning("[UIManager] DisplayChoices: choices or _choiceContainer is null.");
                return;
            }

            foreach (var choice in choices)
            {
                GameObject buttonGO = null;

                bool poolReady =
                    _usePooling &&
                    _objectPooler != null &&
                    _objectPooler.poolDictionary != null &&
                    _objectPooler.poolDictionary.ContainsKey(_choicePoolTag);

                if (poolReady)
                {
                    buttonGO = _objectPooler.SpawnFromPool(_choicePoolTag, Vector3.zero, Quaternion.identity);
                }
                else if (_choiceButtonPrefab != null)
                {
                    buttonGO = Instantiate(_choiceButtonPrefab.gameObject);
                }

                if (buttonGO == null)
                {
                    Debug.LogError("[UIManager] Could not create ChoiceButton (no pool and no prefab).");
                    continue;
                }

                var rt = buttonGO.transform as RectTransform;
                rt.SetParent(_choiceContainer, false);
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition3D = Vector3.zero;
                rt.localScale = Vector3.one;

                var button = buttonGO.GetComponent<ChoiceButton>();
                if (button == null)
                {
                    Debug.LogError("[UIManager] ChoiceButton component missing on spawned object.");
                    continue;
                }

                button.Setup(choice, (selectedChoice) => OnChoiceSelected?.Invoke(selectedChoice));

                // NEW: feed preview data into the button
                var gc = GameController.Instance;
                if (gc != null)
                {
                    var preview = gc.GetChoicePreview(choice);
                    button.SetPreview(preview);
                    Debug.Log($"[UIManager] Preview for '{choice?.name ?? "NULL"}': {preview.Count} items");
                }
                else
                {
                    Debug.LogWarning("[UIManager] GameController.Instance is null — no previews");
                }


                // Call Setup with the correct number of arguments as defined in ChoiceButton.
                button.Setup(choice, (selectedChoice) => OnChoiceSelected?.Invoke(selectedChoice));

                _choiceButtons.Add(button);
            }
        }

        public void UpdateResources(Dictionary<string, float> currentValues)
        {
            if (currentValues == null) return;

            foreach (var kvp in currentValues)
            {
                if (_resourceDisplayMap.TryGetValue(kvp.Key, out var display))
                {
                    display.UpdateDisplay(kvp.Value);
                }
            }
        }

        public void SetNextDayButtonVisible(bool visible)
        {
            if (_nextDayButton != null)
                _nextDayButton.gameObject.SetActive(visible);
        }

        public void CreateResourceDisplays(List<Resource> resources)
        {
            _resourceDisplayMap.Clear();

            if (_resourceContainer == null || _resourceDisplayPrefab == null)
            {
                Log.Warning("[UIManager] CreateResourceDisplays: missing _resourceContainer or _resourceDisplayPrefab.");
                return;
            }

            foreach (Transform child in _resourceContainer)
                Destroy(child.gameObject);

            if (resources == null) return;

            foreach (var resource in resources)
            {
                GameObject go = Instantiate(_resourceDisplayPrefab, _resourceContainer);
                ResourceDisplay display = go.GetComponent<ResourceDisplay>();

                if (display != null && resource != null)
                {
                    display.Setup(resource.Id, resource.MinValue, resource.MaxValue, resource.InitialValue, resource.DisplayName);
                    _resourceDisplayMap[resource.Id] = display;
                }
                else
                {
                    Log.Warning($"[UIManager] ResourceDisplay missing or resource null for entry.");
                }
            }
        }
        [Header("Game Over UI")]
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private TextMeshProUGUI _gameOverTitle;
        [SerializeField] private TextMeshProUGUI _gameOverBody;
        [SerializeField] private Button _restartButton;

        public event Action OnRestartRequested;
        [SerializeField] private Button _rewindButton; // optional, hook in inspector

        public event Action OnRewindRequested;

        private void Start()
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveAllListeners();
                _restartButton.onClick.AddListener(() => OnRestartRequested?.Invoke());
            }
            if (_rewindButton != null)
            {
                _rewindButton.onClick.RemoveAllListeners();
                _rewindButton.onClick.AddListener(() => OnRewindRequested?.Invoke());
            }
        }

        public void ShowGameOver(string title, string body)
        {
            if (_gameOverTitle) _gameOverTitle.text = title ?? "Game Over";
            if (_gameOverBody) _gameOverBody.text = body ?? "";
            if (_gameOverPanel) _gameOverPanel.SetActive(true);

            // Hide interactive day/choices so player focuses on summary
            SetNextDayButtonVisible(false);
            ClearChoicesInternal(); // use your existing ClearChoices (expose a safe internal call)
        }

        // Optional hide if you want to dismiss
        public void HideGameOver()
        {
            if (_gameOverPanel) _gameOverPanel.SetActive(false);
        }

        // Make your existing ClearChoices() callable here without changing public API:
        private void ClearChoicesInternal() => ClearChoices();


    }
}
