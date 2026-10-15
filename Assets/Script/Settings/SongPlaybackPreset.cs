using System;
using System.Globalization;

namespace YARG.Settings
{
    // The key remains in SongKeyOffsets for compatibility with existing installations.
    public sealed class SongPlaybackPreset
    {
        private int _speedPercent = 100;

        public bool VocalsEnabled { get; set; } = true;

        public int SpeedPercent
        {
            get => _speedPercent;
            set => _speedPercent = Math.Clamp(value, 10, 5000);
        }

        public static int ParseSpeedPercent(string text)
        {
            if (!float.TryParse(text?.Trim().TrimEnd('%'), NumberStyles.Number,
                    CultureInfo.CurrentCulture, out float speed) || float.IsNaN(speed) || float.IsInfinity(speed))
                return 100;

            return (int) Math.Clamp(speed, 10, 5000);
        }
    }
}
