using YARG.Localization;
using YARG.Settings;

namespace YARG.Menu.DifficultySelect
{
    public partial class DifficultySelectMenu
    {
        private void CreateVocalsVolumeItem()
        {
            if (_menuState != State.SongSetup || GlobalVariables.State.IsReplay) return;

            string hash = GlobalVariables.State.CurrentSong.Hash.ToString();

            var item = Instantiate(_modifierItemPrefab, _container);
            item.name = "Song vocals";

            void Refresh()
            {
                bool enabled = SettingsManager.GetSongPlaybackPreset(hash).VocalsEnabled;
                string state = Localize.Key(enabled ? "Menu.Common.On" : "Menu.Common.Off");
                item.Initialize(Localize.KeyFormat("Menu.DifficultySelect.SongVocals", state), enabled, Toggle);
            }

            void Toggle(bool enabled)
            {
                if (_menuState != State.SongSetup) return;
                SettingsManager.SetSongVocalsEnabled(hash, enabled);
                Refresh();
                SaveSongPlaybackPreset();
            }

            Refresh();
            _navGroup.AddNavigatable(item);
        }
    }
}
