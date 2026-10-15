using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;

namespace YARG.Menu.DifficultySelect
{
    /// <summary>Uses the same card and round buttons as the surrounding difficulty menu.</summary>
    public sealed class SongKeySelectItem : MonoBehaviour
    {
        private DifficultyItem _item;
        private ColoredButton _minus;
        private ColoredButton _plus;
        private TextMeshProUGUI _value;
        private Func<int> _getKey;
        private Action<int> _setKey;
        private Func<string> _getContext;
        private bool _editing;

        public NavigatableButton Button => _item.Button;

        public static SongKeySelectItem Create(DifficultyItem cardPrefab, ColoredButton buttonPrefab,
            Transform parent, Func<int> getKey, Action<int> setKey, Func<string> getContext)
        {
            var card = Instantiate(cardPrefab, parent);
            card.name = "Song key";
            var row = card.gameObject.AddComponent<SongKeySelectItem>();
            row._item = card;
            row._getKey = getKey;
            row._setKey = setKey;
            row._getContext = getContext;
            card.Initialize(Localize.Key("Menu.SongKey.Label"), "", row.Confirm);
            card.Header.margin = new Vector4(20, 0, 210, 0);
            card.Body.margin = new Vector4(20, 0, 210, 0);
            card.Header.alignment = card.Body.alignment = TextAlignmentOptions.MidlineLeft;

            row._value = Instantiate(card.Body, card.transform);
            row._value.name = "Key offset";
            row._value.margin = Vector4.zero;
            row._value.alignment = TextAlignmentOptions.Center;
            row._value.raycastTarget = false;
            Position(row._value.gameObject, -114, 80, 48);
            row._minus = row.CreateButton(buttonPrefab, "-", -188, -1);
            row._plus = row.CreateButton(buttonPrefab, "+", -40, 1);
            card.Button.SelectionStateChanged += row.SelectionChanged;
            row.Refresh();
            return row;
        }

        private ColoredButton CreateButton(ColoredButton prefab, string label, float x, int delta)
        {
            var button = Instantiate(prefab, transform);
            button.name = label == "-" ? "Lower key" : "Raise key";
            button.Text.text = label;
            button.Text.fontSize = 30;
            Position(button.gameObject, x, 48, 48);
            var inner = button.GetComponentInChildren<Button>();
            var rect = (RectTransform) inner.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2, 2);
            rect.offsetMax = new Vector2(-2, -2);
            button.OnClick.AddListener(() =>
            {
                Button.SetSelected(true, SelectionOrigin.Mouse);
                Adjust(delta);
            });
            return button;
        }

        private static void Position(GameObject obj, float x, float width, float height)
        {
            var layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var rect = (RectTransform) obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, 0);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void Refresh()
        {
            int key = _getKey();
            _value.text = key.ToString("+0;-0;0", CultureInfo.InvariantCulture);
            _item.Body.text = _getContext();
            if (key > SongKey.MIN_SEMITONES) _minus.EnableButton(); else _minus.DisableButton();
            if (key < SongKey.MAX_SEMITONES) _plus.EnableButton(); else _plus.DisableButton();
        }

        private void Adjust(int delta)
        {
            _setKey(SongKey.Clamp(_getKey() + delta));
            Refresh();
        }

        private void Confirm()
        {
            if (_editing) return;
            _editing = true;
            var scheme = new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", Finish),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Confirm", Finish),
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase", () => Adjust(1)),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease", () => Adjust(-1)),
                new NavigationScheme.Entry(MenuAction.Left, "Menu.Common.Decrease", () => Adjust(-1), hide: true),
                new NavigationScheme.Entry(MenuAction.Right, "Menu.Common.Increase", () => Adjust(1), hide: true),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.SongKey.Reset", () => { _setKey(0); Refresh(); })
            }, true);
            scheme.PopCallback = () => _editing = false;
            Navigator.Instance.PushScheme(scheme);
        }

        private void SelectionChanged(NavigatableBehaviour row, bool selected, SelectionOrigin origin)
        {
            if (!selected) Finish();
        }

        private void Finish()
        {
            if (_editing) Navigator.Instance.PopScheme();
        }

        private void OnDisable() => Finish();
        private void OnDestroy()
        {
            if (_item != null) _item.Button.SelectionStateChanged -= SelectionChanged;
        }
    }
}
