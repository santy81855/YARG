using System;
using YARG.Audio.BASS;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        public int SongKeySemitones { get; private set; }

        // Replay inputs are stored in chart pitch, so replays use the original key.
        public bool CanChangeSongKey => ReplayInfo == null && _mixer is BassStemMixer;

        public event Action SongKeyChanged;

        private void InitializeSongKey()
        {
            if (ReplayInfo == null && SettingsManager.Settings.SongKeyOffsets != null &&
                SettingsManager.Settings.SongKeyOffsets.TryGetValue(Song.Hash.ToString(), out int semitones))
            {
                SongKeySemitones = SongKey.Clamp(semitones);
            }
        }

        public bool SetSongKey(int semitones)
        {
            if (!Paused || !CanChangeSongKey)
            {
                return false;
            }

            semitones = SongKey.Clamp(semitones);
            if (semitones == SongKeySemitones)
            {
                return true;
            }

            var mixer = (BassStemMixer) _mixer;
            if (!mixer.SetSongKey(semitones))
            {
                ToastManager.ToastError(Localize.Key("Menu.SongKey.AudioError"));
                return false;
            }

            // Discard audio buffered in the old key without moving the song clock.
            mixer.SetPosition(mixer.GetPosition());
            SongKeySemitones = semitones;
            VocalTrack.RefreshSongKey();
            SongKeyChanged?.Invoke();

            var offsets = SettingsManager.Settings.SongKeyOffsets ??= new();
            string hash = Song.Hash.ToString();
            if (semitones == 0)
            {
                offsets.Remove(hash);
            }
            else
            {
                offsets[hash] = semitones;
            }

            try
            {
                SettingsManager.SaveSettings();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to save the song key");
                ToastManager.ToastError(Localize.Key("Menu.SongKey.SaveError"));
            }

            return true;
        }
    }
}