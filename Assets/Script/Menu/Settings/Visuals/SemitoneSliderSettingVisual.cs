// Assets/Script/Menu/Settings/Visuals/SemitoneSliderSettingVisual.cs
using UnityEngine;
using TMPro;
using YARG.Core.Input;
using YARG.Menu;                     // <-- SemitoneValueSlider lives here
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class SemitoneSliderSettingVisual : BaseSettingVisual<SliderSetting>
    {
        [SerializeField] private SemitoneValueSlider _slider; // <-- use the int slider
        [SerializeField] private TMP_Text _valueLabel;

        private bool _ignoreCallback;

        protected override void OnSettingInit()
        {
            _ignoreCallback = true;

            // Setting.Min/Max are float -> cast to int for our int-based slider
            _slider.MinimumValue = Mathf.RoundToInt(Setting.Min);
            _slider.MaximumValue = Mathf.RoundToInt(Setting.Max);

            // Initialize current value (rounded)
            var iv = Mathf.RoundToInt(Setting.Value);
            _slider.SetValueWithoutNotify(iv);
            UpdateLabel(iv);

            _ignoreCallback = false;

            base.OnSettingInit();
        }

        protected override void RefreshVisual()
        {
            var iv = Mathf.RoundToInt(Setting.Value);
            _slider.SetValueWithoutNotify(iv);
            UpdateLabel(iv);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase", () =>
                {
                    var step = Mathf.Max(1, Mathf.RoundToInt((Setting.Max - Setting.Min) / 20f));
                    Setting.Value = Mathf.RoundToInt(Setting.Value) + step;
                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease", () =>
                {
                    var step = Mathf.Max(1, Mathf.RoundToInt((Setting.Max - Setting.Min) / 20f));
                    Setting.Value = Mathf.RoundToInt(Setting.Value) - step;
                    RefreshVisual();
                })
            }, true);
        }

        // Hook this in the prefab to SemitoneValueSlider.ValueChanged
        public void OnValueChange()
        {
            if (_ignoreCallback) return;

            int iv = _slider.Value;            // already int
            Setting.Value = iv;                // Setting is float, implicit cast ok via setter
            UpdateLabel(iv);
        }

        private void UpdateLabel(int iv)
        {
            if (_valueLabel != null)
                _valueLabel.text = iv.ToString("+#;-#;0");
        }
    }
}
