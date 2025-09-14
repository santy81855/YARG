using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace YARG.Menu
{
    [ExecuteAlways]
    public class SemitoneValueSlider : MonoBehaviour
    {
        [SerializeField]
        private Slider _slider;
        [SerializeField]
        private TMP_InputField _inputField;
        [SerializeField]
        private string _formatString = "+0;-0;0"; 
        // +0 shows positive values with a "+" sign, negatives with "-", 0 stays 0

        [Space]
        public UnityEvent<int> ValueChanged;

        public bool ClampMin = true;
        public bool ClampMax = true;

        private int _value = 0;
        public int Value
        {
            get => _value;
            set => SetValue(value, notify: true);
        }

        public int MinimumValue
        {
            get => Mathf.RoundToInt(_slider.minValue);
            set => _slider.minValue = value;
        }

        public int MaximumValue
        {
            get => Mathf.RoundToInt(_slider.maxValue);
            set => _slider.maxValue = value;
        }

        private string _beforeEditText;
        private int _beforeEditValue;

        // TMP_InputField.SetTextWithoutNotify still notifies when in editor
        private bool _isSettingValue;

        public void OnSelect()
        {
            if (_isSettingValue)
                return;

            _beforeEditText = _inputField.text;
            _beforeEditValue = _value;
        }

        public void OnDeselect()
        {
            if (_isSettingValue)
                return;

            _beforeEditText = null;
        }

        public void OnSliderChange(float value)
        {
            int intValue = Mathf.RoundToInt(value);

            if (_value == intValue)
                return;

            SetValue(intValue, setSlider: false, notify: true);
        }

        public void OnTextChanged(string text)
        {
            if (_isSettingValue || !int.TryParse(text, out int value))
                return;

            // Update internal value silently while typing
            SetValue(value, setValue: false, setText: false, notify: false);
        }

        public void OnFinishEdit(string text)
        {
            if (_isSettingValue)
                return;

            if (!int.TryParse(text, out int value))
            {
                // Reset to previous valid state on invalid input
                _isSettingValue = true;

                _inputField.SetTextWithoutNotify(_beforeEditText);
                _slider.SetValueWithoutNotify(_beforeEditValue);

                _isSettingValue = false;
                return;
            }

            SetValue(value, notify: true);
        }

        private int ClampValue(int value)
        {
            if (ClampMin)
                value = Math.Max(value, MinimumValue);

            if (ClampMax)
                value = Math.Min(value, MaximumValue);

            return value;
        }

        private void SetValue(int value, bool notify,
            bool setSlider = true, bool setValue = true, bool setText = true)
        {
            if (_isSettingValue)
                return;

            _isSettingValue = true;

            value = ClampValue(value);

            if (setSlider)
                _slider.SetValueWithoutNotify(value);

            if (setValue)
                _value = value;

            if (setText)
                _inputField.SetTextWithoutNotify(value.ToString(_formatString));

            if (notify)
                ValueChanged.Invoke(value);

            _isSettingValue = false;
        }

        public void SetValueWithoutNotify(int value)
        {
            SetValue(value, notify: false);
        }
    }
}
