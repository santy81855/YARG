using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public sealed class SongKeyPauseSetting : NavigatableBehaviour, IScrollHandler
    {
        [SerializeField]
        private GameObject _activeBackground;
        [SerializeField]
        private TextMeshProUGUI _text;
        [SerializeField]
        private TextMeshProUGUI _value;
        [SerializeField]
        private Button _decreaseButton;
        [SerializeField]
        private Button _increaseButton;

        private GameManager _gameManager;
        private bool _editing;

        public void Initialize(GameManager gameManager)
        {
            _gameManager = gameManager;
            _text.text = Localize.Key("Menu.SongKey.Label");
            _decreaseButton.onClick.AddListener(Decrease);
            _increaseButton.onClick.AddListener(Increase);
            _gameManager.SongKeyChanged += Refresh;
            Refresh();
        }

        public override void Confirm()
        {
            if (_editing) return;

            var scheme = new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", FinishEditing),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Confirm", FinishEditing),
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase", () => Adjust(1)),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease", () => Adjust(-1)),
                new NavigationScheme.Entry(MenuAction.Left, "Menu.Common.Decrease", () => Adjust(-1), hide: true),
                new NavigationScheme.Entry(MenuAction.Right, "Menu.Common.Increase", () => Adjust(1), hide: true),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.SongKey.Reset", () => SetKey(0)),
            }, true);
            scheme.PopCallback = () =>
            {
                _editing = false;
                _activeBackground.SetActive(false);
            };

            _editing = true;
            _activeBackground.SetActive(true);
            Navigator.Instance.PushScheme(scheme);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left) Confirm();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData.scrollDelta.y != 0)
            {
                Adjust(eventData.scrollDelta.y > 0 ? 1 : -1);
            }
        }

        private void Adjust(int amount) => SetKey(_gameManager.SongKeySemitones + amount);

        private void Decrease()
        {
            SetSelected(true, SelectionOrigin.Mouse);
            Adjust(-1);
        }

        private void Increase()
        {
            SetSelected(true, SelectionOrigin.Mouse);
            Adjust(1);
        }

        private void SetKey(int semitones)
        {
            _gameManager.SetSongKey(semitones);
            Refresh();
        }

        private void Refresh()
        {
            int semitones = _gameManager.SongKeySemitones;
            _value.text = semitones.ToString("+0;-0;0", CultureInfo.InvariantCulture);
            _decreaseButton.interactable = semitones > SongKey.MIN_SEMITONES;
            _increaseButton.interactable = semitones < SongKey.MAX_SEMITONES;
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);
            if (!selected) FinishEditing();
        }

        private void FinishEditing()
        {
            if (_editing) Navigator.Instance.PopScheme();
        }

        private void OnDisable() => FinishEditing();

        protected override void OnDestroy()
        {
            if (_gameManager != null) _gameManager.SongKeyChanged -= Refresh;
            if (_decreaseButton != null) _decreaseButton.onClick.RemoveListener(Decrease);
            if (_increaseButton != null) _increaseButton.onClick.RemoveListener(Increase);
            base.OnDestroy();
        }
    }
}