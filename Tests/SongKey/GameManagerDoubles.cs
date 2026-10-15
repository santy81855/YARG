// Stand-ins for Unity-owned services. The production GameManager.SongKey.cs is
// compiled unchanged so these checks exercise its persistence and failure paths.
namespace YARG.Audio.BASS
{
    public class BassStemMixer
    {
        public bool Succeed = true;
        public int Key;
        public int Seeks;
        public bool SetSongKey(int key)
        {
            if (!Succeed) return false;
            Key = key;
            return true;
        }
        public double GetPosition() => 7.5;
        public void SetPosition(double position)
        {
            if (position != 7.5) throw new Exception("The song clock moved");
            Seeks++;
        }
    }
}

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private readonly object _mixer = new Audio.BASS.BassStemMixer();
        public Audio.BASS.BassStemMixer Mixer => (Audio.BASS.BassStemMixer) _mixer;
        public bool Paused = true;
        public object ReplayInfo;
        public FakeSong Song = new();
        public FakeVocalTrack VocalTrack = new();
        public void LoadKey() => InitializeSongKey();
    }
    public class FakeSong { public string Hash = "song-one"; }
    public class FakeVocalTrack { public int Refreshes; public void RefreshSongKey() => Refreshes++; }
}

namespace YARG.Settings
{
    public static class SettingsManager
    {
        public sealed class SettingContainer
        {
            public Dictionary<string, int> SongKeyOffsets { get; set; } = new();
        }
        public static SettingContainer Settings = new();
        public static int Saves;
        public static bool FailSave;
        public static string LastSavedJson;
        public static void SaveSettings()
        {
            if (FailSave) throw new IOException("Simulated save failure");
            LastSavedJson = Newtonsoft.Json.JsonConvert.SerializeObject(Settings);
            Saves++;
        }
    }
}

namespace YARG.Localization
{
    public static class Localize { public static string Key(string key) => key; }
}

namespace YARG.Menu.Persistent
{
    public static class ToastManager
    {
        public static string LastError;
        public static void ToastError(string text) => LastError = text;
    }
}
