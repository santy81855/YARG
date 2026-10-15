using System;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YARG.Menu.DifficultySelect
{
    public partial class DifficultySelectMenu
    {
        [SerializeField]
        private ColoredButton _songKeyButtonPrefab;

        private SongEntry _pitchPreviewSong;
        private Task<VocalPitchPreview> _pitchPreviewTask;
        private SongKeySelectItem _songKeyRow;

        private void CreateSongKeyItem()
        {
            if (CurrentPlayer.Profile.GameMode != GameMode.Vocals || GlobalVariables.State.IsReplay ||
                GlobalVariables.State.PlayingAShow) return;

            var song = GlobalVariables.State.CurrentSong;
            if (_pitchPreviewSong != song || _pitchPreviewTask == null)
            {
                _pitchPreviewSong = song;
                _pitchPreviewTask = Task.Run(() => new VocalPitchPreview(song.LoadChart()));
            }
            _songKeyRow = SongKeySelectItem.Create(_difficultyItemPrefab, _songKeyButtonPrefab, _container,
                GetSelectedSongKey, SetSelectedSongKey, KeyContext);
            _navGroup.AddNavigatable(_songKeyRow.Button);
            _ = RefreshPitchPreview(_pitchPreviewTask, song);
        }

        private async Task RefreshPitchPreview(Task<VocalPitchPreview> task, SongEntry song)
        {
            try { await task; }
            catch (Exception e) { YargLogger.LogException(e, "Could not read the song's vocal range"); }
            if (this != null && isActiveAndEnabled && _playerIndex < YARG.Player.PlayerContainer.Players.Count &&
                _pitchPreviewSong == song && _songKeyRow != null)
                _songKeyRow.Refresh();
        }

        private int GetSelectedSongKey()
        {
            string hash = GlobalVariables.State.CurrentSong.Hash.ToString();
            return SettingsManager.Settings.SongKeyOffsets != null &&
                SettingsManager.Settings.SongKeyOffsets.TryGetValue(hash, out int key) ? SongKey.Clamp(key) : 0;
        }

        private void SetSelectedSongKey(int key)
        {
            key = SongKey.Clamp(key);
            if (key == GetSelectedSongKey()) return;
            string hash = GlobalVariables.State.CurrentSong.Hash.ToString();
            var offsets = SettingsManager.Settings.SongKeyOffsets ??= new();
            if (key == 0) offsets.Remove(hash);
            else offsets[hash] = key;
            try { SettingsManager.SaveSettings(); }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Could not save the song key");
                ToastManager.ToastError(Localize.Key("Menu.SongKey.SaveError"));
            }
        }

        private string KeyContext()
        {
            if (CurrentPlayer.Profile.IsModifierActive(Modifier.UnpitchedOnly))
                return Localize.Key("Menu.SongKey.Unpitched");
            if (_pitchPreviewTask == null || !_pitchPreviewTask.IsCompleted)
                return Localize.Key("Menu.SongKey.LoadingNotes");
            if (_pitchPreviewTask.Status != TaskStatus.RanToCompletion)
                return Localize.Key("Menu.SongKey.NotesUnavailable");
            var profile = CurrentPlayer.Profile;
            float? highest = _pitchPreviewTask.Result.Highest(profile.CurrentInstrument, profile.HarmonyIndex);
            return highest.HasValue ? Localize.KeyFormat("Menu.SongKey.HighestNote",
                VocalPitchPreview.NoteName(highest.Value + GetSelectedSongKey())) : Localize.Key("Menu.SongKey.Unpitched");
        }
    }
}
