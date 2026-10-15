using System;
using YARG.Core.Logging;
using YARG.Localization;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YARG.Menu.DifficultySelect
{
    public partial class DifficultySelectMenu
    {
        private float _selectedSongSpeed = 1f;

        private bool CanSaveSongSpeed => !GlobalVariables.State.IsReplay &&
            !GlobalVariables.State.PlayingAShow &&
            YARG.Online.OnlineSession.Current is not { IsConnected: true, IsHost: false };

        private void LoadSongPlaybackPreset()
        {
            string hash = GlobalVariables.State.CurrentSong.Hash.ToString();
            var preset = SettingsManager.GetSongPlaybackPreset(hash);
            float speed = !GlobalVariables.State.PlayingAShow && !GlobalVariables.State.IsReplay
                ? preset.SpeedPercent / 100f : 1f;
            if (YARG.Online.OnlineSession.Current is { IsConnected: true, IsHost: false } room)
                speed = room.SelectedSpeed;

            _selectedSongSpeed = speed;

            _speedInput.interactable = YARG.Online.OnlineSession.Current is not { IsConnected: true, IsHost: false };
            _speedInput.SetTextWithoutNotify($"{UnityEngine.Mathf.RoundToInt(speed * 100f)}%");
            if (!GlobalVariables.State.IsReplay) SettingsManager.ApplySongVocalsPreset(hash);
        }

        private void SaveSongPlaybackPreset()
        {
            try { SettingsManager.SaveSettings(); }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Could not save the song playback preset");
                ToastManager.ToastError(Localize.Key("Menu.DifficultySelect.PresetSaveError"));
            }
        }

        private float CommitSongSpeed(string text)
        {
            // Guests use the host's speed without replacing their own saved preference.
            if (YARG.Online.OnlineSession.Current is { IsConnected: true, IsHost: false } room)
            {
                _speedInput.SetTextWithoutNotify($"{UnityEngine.Mathf.RoundToInt(room.SelectedSpeed * 100f)}%");
                return room.SelectedSpeed;
            }

            // Delayed field events and later player menus cannot change the shared choice.
            if (_menuState != State.SongSetup) return _selectedSongSpeed;

            int percent = SongPlaybackPreset.ParseSpeedPercent(text);
            _selectedSongSpeed = percent / 100f;
            _speedInput.SetTextWithoutNotify($"{percent}%");
            if (CanSaveSongSpeed)
            {
                string hash = GlobalVariables.State.CurrentSong.Hash.ToString();
                SettingsManager.SetSongSpeedPercent(hash, percent);
                SaveSongPlaybackPreset();
            }
            return _selectedSongSpeed;
        }
    }
}
