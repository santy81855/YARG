using System.Globalization;
using Newtonsoft.Json;
using YARG;
using YARG.Core.Audio;
using YARG.Menu.DifficultySelect;
using YARG.Menu.Persistent;
using YARG.Online;
using YARG.Settings;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}

// Old settings load without migration, retaining the previously saved key.
SettingsManager.Settings = JsonConvert.DeserializeObject<SettingsManager.SettingContainer>(
    "{\"SongKeyOffsets\":{\"song-a\":-3}}");
var menu = new DifficultySelectMenu();
menu.Open();
Check(menu.SpeedText == "100%" && menu.LastToggle.Enabled, "Old settings did not receive safe defaults");
Check(SettingsManager.Settings.SongKeyOffsets["song-a"] == -3, "The existing key was lost");
Check(GlobalAudioHandler.Volume == 1, "Vocals On must be exactly 100%");
Check(SettingsManager.Settings.VocalsVolume.Value == .37f, "Opening a song changed global audio preferences");

menu.LastToggle.Toggle(false);
Check(!menu.LastToggle.Enabled && GlobalAudioHandler.Volume == 0, "Vocals Off did not mute playback");
Check(menu.Commit("85%") == .85f, "Speed did not commit on Ready");
Check(SettingsManager.GetSongPlaybackPreset("song-a").SpeedPercent == 85, "Speed was not stored");
Check(!SettingsManager.GetSongPlaybackPreset("song-a").VocalsEnabled, "Editing speed reset vocals");

// Reload the saved JSON as a fresh installation run would.
SettingsManager.Settings = JsonConvert.DeserializeObject<SettingsManager.SettingContainer>(SettingsManager.LastSavedJson);
GlobalVariables.State.CurrentSong.Hash = "song-b";
menu.Open();
Check(menu.SpeedText == "100%" && menu.LastToggle.Enabled && GlobalAudioHandler.Volume == 1,
    "Song A's choices leaked into song B");
menu.Commit("120");
GlobalVariables.State.CurrentSong.Hash = "song-a";
menu.Open();
Check(menu.SpeedText == "85%" && !menu.LastToggle.Enabled && GlobalAudioHandler.Volume == 0,
    "Song A's saved choices were not restored");
Check(SettingsManager.Settings.SongKeyOffsets["song-a"] == -3, "Saving playback settings changed the key");
menu.LastToggle.Toggle(true);
Check(GlobalAudioHandler.Volume == 1 && SettingsManager.GetSongPlaybackPreset("song-a").SpeedPercent == 85,
    "Editing vocals reset speed");

// Online speed belongs to the host; guests retain their offline preference.
OnlineSession.Current = new() { IsConnected = true, IsHost = false, SelectedSpeed = .9f };
menu.Open();
Check(menu.SpeedText == "90%" && !menu.SpeedEditable, "A guest can edit the host's speed");
Check(menu.Commit("200") == .9f && SettingsManager.GetSongPlaybackPreset("song-a").SpeedPercent == 85,
    "The host's speed replaced the guest's saved speed");
menu.LastToggle.Toggle(false);
Check(GlobalAudioHandler.Volume == 0, "The guest cannot choose their own vocals setting");
OnlineSession.Current.IsHost = true;
menu.Open();
Check(menu.SpeedText == "85%" && menu.SpeedEditable, "Host did not load their own saved speed");
menu.Commit("95%");
Check(SettingsManager.GetSongPlaybackPreset("song-a").SpeedPercent == 95, "Host speed was not saved");
OnlineSession.Current = null;

GlobalVariables.State.PlayingAShow = true;
menu.Open();
menu.Commit("70%");
Check(SettingsManager.GetSongPlaybackPreset("song-a").SpeedPercent == 95, "Show speed overwrote a song preset");
GlobalVariables.State.PlayingAShow = false;
GlobalVariables.State.IsReplay = true;
menu.Commit("60%");
Check(SettingsManager.GetSongPlaybackPreset("song-a").SpeedPercent == 95, "Replay speed overwrote a song preset");
GlobalVariables.State.IsReplay = false;

// A damaged or hand-edited settings file must not cause invalid audio speeds.
foreach (var pair in new[] { ("", 100), ("bad", 100), ("NaN", 100), ("Infinity", 100),
    ("-20", 10), ("999999", 5000), ("85.9%", 85), (" 90% ", 90) })
{
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    Check(SongPlaybackPreset.ParseSpeedPercent(pair.Item1) == pair.Item2, "Invalid speed parsing: " + pair.Item1);
}
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
Check(SongPlaybackPreset.ParseSpeedPercent("85,5%") == 85, "Local decimal separators are not supported");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var corrupt = JsonConvert.DeserializeObject<SongPlaybackPreset>("{\"SpeedPercent\":-200}");
Check(corrupt.SpeedPercent == 10, "A corrupt stored speed was not clamped");
SettingsManager.Settings.SongPlaybackPresets = null;
Check(SettingsManager.GetSongPlaybackPreset("missing").SpeedPercent == 100, "A null preset collection was not handled");
SettingsManager.SetSongSpeedPercent("missing", 50000);
Check(SettingsManager.GetSongPlaybackPreset("missing").SpeedPercent == 5000, "Saving an invalid speed did not clamp it");
SettingsManager.Settings.SongPlaybackPresets["missing"] = null;
SettingsManager.SetSongVocalsEnabled("missing", false);
Check(!SettingsManager.GetSongPlaybackPreset("missing").VocalsEnabled, "A null preset entry was not repaired");

SettingsManager.ClearSongVocalsPreset();
Check(Math.Abs(GlobalAudioHandler.Volume - .37f) < .00001, "Leaving the song did not restore global volume");
SettingsManager.ApplySongVocalsPreset("missing");
Check(SettingsManager.GetEffectiveVocalVolume(.37f) == 0, "Mute-on-miss would restore the wrong vocal volume");
SettingsManager.SetGlobalVocalsVolume(.5f);
Check(SettingsManager.GetEffectiveVocalVolume(.5f) == .5f && GlobalAudioHandler.Volume == .5,
    "Explicit audio-menu changes did not override the active preset");

SettingsManager.FailSave = true;
menu.Open();
menu.LastToggle.Toggle(false);
Check(ToastManager.Errors == 1 && GlobalAudioHandler.Volume == 0, "A failed save silently lost feedback or playback changes");
SettingsManager.FailSave = false;

// Shared setup precedes all local players; later menus cannot change its choices.
YARG.Player.PlayerContainer.Players.Add(new object());
menu.Open();
Check(menu.InSongSetup && menu.Header == "SongSetup" && menu.SongKeyRows == 1 && menu.LastToggle != null,
    "Shared song setup is missing its controls");
var previousToggle = menu.LastToggle;
previousToggle.Toggle(false);
menu.SetSpeedText("80%");
menu.Continue();
Check(!menu.InSongSetup && menu.PlayerIndex == 0, "Continue skipped the first player");
Check(menu.SongKeyRows == 0 && menu.LastToggle == null, "Shared controls leaked into a player menu");
Check(menu.Commit("150%") == .8f, "A delayed field event changed the committed speed");
previousToggle.Toggle(true);
Check(!SettingsManager.GetSongPlaybackPreset("song-a").VocalsEnabled,
    "An obsolete toggle callback changed the shared choice after Continue");
menu.NextPlayer();
Check(menu.PlayerIndex == 1 && !menu.InSongSetup && menu.LastToggle == null, "Player two received shared controls");
Check(menu.Commit("200%") == .8f, "Player two replaced the shared speed");
menu.Back();
Check(menu.PlayerIndex == 0 && !menu.InSongSetup, "Back from player two did not return to player one");
menu.EnterDifficulty();
menu.Back();
Check(menu.PlayerIndex == 0 && !menu.InSongSetup, "Back from a submenu reopened shared setup");
menu.Back();
Check(menu.InSongSetup && menu.SpeedText == "80%" && !menu.LastToggle.Enabled,
    "Back from player one did not restore shared setup and its choices");
menu.SetSpeedText("90%");
menu.Continue();
Check(menu.Commit("200%") == .9f, "Returning to shared setup did not allow an intentional speed change");
menu.Back();
int pops = YARG.Menu.MenuManager.Instance.Pops;
menu.Back();
Check(YARG.Menu.MenuManager.Instance.Pops == pops + 1, "Back from shared setup did not return to the library");

// Shared audio choices belong to the song, even when the first player uses guitar.
menu.SetGameMode(YARG.Core.GameMode.FiveFretGuitar);
menu.Open();
Check(menu.LastToggle != null && menu.SongKeyRows == 1, "Shared setup depends on the first player's microphone profile");
OnlineSession.Current = new() { IsConnected = true, IsHost = false, SelectedSpeed = .75f };
menu.Open();
Check(menu.InSongSetup && !menu.SpeedEditable && menu.LastToggle != null,
    "The online guest did not get their own shared audio setup");
menu.SetSpeedText("300%");
menu.Continue();
Check(menu.Commit("300%") == .75f, "Shared setup ignored the online host's speed");
menu.Back();
menu.Back();
Check(OnlineSession.Current.ReturnedToRoom, "Back from online shared setup did not return to the room");
OnlineSession.Current.ReturnedToRoom = false;
menu.Open();
menu.WaitOnline();
menu.Back();
Check(OnlineSession.Current.ReturnedToRoom, "Back while waiting online failed");
Console.WriteLine($"SONG_PRESETS_PASS: {checks} checks (persistence, shared setup, multiple players, audio, online, save failure)");
