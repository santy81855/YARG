// Unity services are replaced, while the production preset storage and menu handlers
// are compiled unchanged. No checks read or write the player's real settings.
using Newtonsoft.Json;
using YARG.Settings;

namespace YARG.Core
{
    public enum GameMode { Vocals, FiveFretGuitar }
}

namespace YARG.Core.Audio
{
    public enum SongStem { Vocals }
    public static class GlobalAudioHandler
    {
        public static double Volume;
        public static void SetVolumeSetting(SongStem stem, double volume) => Volume = volume;
    }
}

namespace YARG.Core.Logging
{
    public static class YargLogger
    {
        public static void LogException(Exception exception, string message) { }
    }
}

namespace YARG.Localization
{
    public static class Localize
    {
        public static string Key(string key) => key;
        public static string KeyFormat(string key, string value) => key + ": " + value;
    }
}

namespace YARG.Menu.Persistent
{
    public static class ToastManager
    {
        public static int Errors;
        public static void ToastError(string message) => Errors++;
    }
}

namespace UnityEngine
{
    public static class Mathf
    {
        public static int RoundToInt(float value) => (int) MathF.Round(value);
    }
}

namespace YARG
{
    public static class GlobalVariables
    {
        public static GameState State = new();
        public sealed class GameState
        {
            public bool IsReplay;
            public bool PlayingAShow;
            public FakeSong CurrentSong = new();
        }
    }
    public sealed class FakeSong { public string Hash = "song-a"; }
}

namespace YARG.Online
{
    public sealed class OnlineSession
    {
        public static OnlineSession Current;
        public bool IsConnected;
        public bool IsHost;
        public float SelectedSpeed;
        public bool ReturnedToRoom;
        public void ReturnToRoom() => ReturnedToRoom = true;
    }
}

namespace YARG.Player
{
    public static class PlayerContainer
    {
        public static List<object> Players = new() { new object() };
    }
}

namespace YARG.Menu
{
    public sealed class MenuManager
    {
        public static MenuManager Instance = new();
        public int Pops;
        public void PopMenu() => Pops++;
    }
}

namespace YARG.Settings
{
    public static partial class SettingsManager
    {
        public sealed class SettingContainer
        {
            public Dictionary<string, int> SongKeyOffsets { get; set; } = new();
            public Dictionary<string, SongPlaybackPreset> SongPlaybackPresets { get; set; } = new();
            public FakeVolume VocalsVolume { get; } = new();
        }
        public sealed class FakeVolume { public float Value = .37f; }
        public static SettingContainer Settings = new();
        public static string LastSavedJson;
        public static bool FailSave;
        public static void SaveSettings()
        {
            if (FailSave) throw new IOException("Simulated save failure");
            LastSavedJson = JsonConvert.SerializeObject(Settings);
        }
    }
}

namespace YARG.Menu.DifficultySelect
{
    public partial class DifficultySelectMenu
    {
        private enum State { SongSetup, Main, Instrument, Difficulty, Modifiers, Harmony }
        private State _menuState;
        private State _lastMenuState;
        private int _playerIndex;
        private readonly FakeInput _speedInput = new();
        private readonly FakePlayer CurrentPlayer = new();
        private readonly FakeText _text = new();
        private readonly FakeText _loadingPhrase = new();
        private readonly object _difficultyGreenPrefab = new();
        private readonly object _modifierItemPrefab = new();
        private readonly object _container = new();
        private readonly FakeNavigation _navGroup = new();
        public ModifierItem LastToggle;
        public string SpeedText => _speedInput.text;
        public bool SpeedEditable => _speedInput.interactable;
        public bool InSongSetup => _menuState == State.SongSetup;
        public int PlayerIndex => _playerIndex;
        public int SongKeyRows;
        public string Header => _text.text;
        public void SetSpeedText(string value) => _speedInput.text = value;
        public void SetGameMode(YARG.Core.GameMode mode) => CurrentPlayer.Profile.GameMode = mode;
        public void Continue() => ContinueFromSongSetup();
        public void Back() => NavigateBack();
        public void NextPlayer() => ChangePlayer(1);
        public void EnterDifficulty() => _menuState = State.Difficulty;
        public void WaitOnline() => _playerIndex = YARG.Player.PlayerContainer.Players.Count;
        public void Open()
        {
            _playerIndex = 0;
            LoadSongPlaybackPreset();
            ShowSongSetup();
        }
        public float Commit(string text) => CommitSongSpeed(text);
        private ModifierItem Instantiate(object prefab, object container) => LastToggle = new();
        private void UpdateForPlayer()
        {
            LastToggle = null;
            SongKeyRows = 0;
            if (_menuState == State.SongSetup) CreateSongSetupMenu();
        }
        private void ChangePlayer(int add)
        {
            _playerIndex += add;
            _menuState = State.Main;
            UpdateForPlayer();
        }
        private void CreateSongKeyItem() => SongKeyRows++;
        private void CreateItem(string label, bool selected, object prefab, Action action) { }
        private string LocalizeHeader(string key) => key;
        private void ShowWarning(string message) { }
    }
    public sealed class FakeText { public string text; }
    public sealed class FakeInput
    {
        public string text;
        public bool interactable;
        public void SetTextWithoutNotify(string value) => text = value;
    }
    public sealed class FakeNavigation { public void AddNavigatable(ModifierItem item) { } }
    public sealed class FakePlayer { public FakeProfile Profile = new(); }
    public sealed class FakeProfile { public YARG.Core.GameMode GameMode = YARG.Core.GameMode.Vocals; }
    public sealed class ModifierItem
    {
        public string name;
        public bool Enabled;
        public Action<bool> Toggle;
        public void Initialize(string title, bool enabled, Action<bool> toggle)
        {
            Enabled = enabled;
            Toggle = toggle;
        }
    }
}
