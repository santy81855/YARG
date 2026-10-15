using YARG.Core.Audio;

namespace YARG.Settings
{
    public static partial class SettingsManager
    {
        private static float? _songVocalVolume;

        public static SongPlaybackPreset GetSongPlaybackPreset(string hash)
        {
            return Settings.SongPlaybackPresets != null &&
                Settings.SongPlaybackPresets.TryGetValue(hash, out var preset) && preset != null
                    ? preset : new SongPlaybackPreset();
        }

        public static void SetSongVocalsEnabled(string hash, bool enabled)
        {
            var presets = Settings.SongPlaybackPresets ??= new();
            var preset = GetSongPlaybackPreset(hash);
            preset.VocalsEnabled = enabled;
            presets[hash] = preset;
            ApplySongVocalsPreset(hash);
        }

        public static void SetSongSpeedPercent(string hash, int speedPercent)
        {
            var presets = Settings.SongPlaybackPresets ??= new();
            var preset = GetSongPlaybackPreset(hash);
            preset.SpeedPercent = speedPercent;
            presets[hash] = preset;
        }

        public static void ApplySongVocalsPreset(string hash)
        {
            _songVocalVolume = GetSongPlaybackPreset(hash).VocalsEnabled ? 1f : 0f;
            GlobalAudioHandler.SetVolumeSetting(SongStem.Vocals, _songVocalVolume.Value);
        }

        public static float GetEffectiveVocalVolume(float globalVolume) => _songVocalVolume ?? globalVolume;

        // An explicit change in the audio settings menu still takes effect during a song.
        public static void SetGlobalVocalsVolume(float volume)
        {
            _songVocalVolume = null;
            GlobalAudioHandler.SetVolumeSetting(SongStem.Vocals, volume);
        }

        public static void ClearSongVocalsPreset()
        {
            SetGlobalVocalsVolume(Settings.VocalsVolume.Value);
        }
    }
}
