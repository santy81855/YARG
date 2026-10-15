using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Online
{
    public sealed partial class OnlineSession
    {
        private readonly List<YargPlayer> _roundPlayers = new();
        private readonly List<YargPlayer> _localPlayers = new();
        private readonly List<YargPlayer> _remotePlayers = new();
        private readonly Dictionary<BasePlayer, int> _sentInputs = new();
        private OnlineProfile[] _localProfiles;
        private OnlineProfile[] _remoteProfiles;
        private GameManager _game;
        private bool _localLoaded;
        private bool _remoteLoaded;
        private bool _localFinished;
        private bool _remoteFinished;
        private OnlineFinalScore[] _remoteResults;
        private double _finishedAt;
        private bool _startScheduled;
        private OnlineMessage _scheduledControl;
        private double _lastFrame;
        private double _loadStarted;
        private float _speed = 1;


        public float SelectedSpeed => _speed;

        public YargProfile HostVocalProfile => IsHost ? null : _remoteProfiles?
            .Select(p => p.Profile).FirstOrDefault(p => p.GameMode == GameMode.Vocals);

        public void Ready(float speed)
        {
            if (!_selecting || _localProfiles != null) return;
            try
            {
                _localPlayers.Clear();
                _localPlayers.AddRange(PlayerContainer.Players.Where(p => !p.SittingOut));
                if (_localPlayers.Count == 0 || _localPlayers.Count > 4)
                    throw new InvalidOperationException("Choose between one and four local players for this room.");
                _localProfiles = _localPlayers.Select(p =>
                {
                    if (p.Profile.GameMode == GameMode.Vocals && HostVocalProfile != null)
                        p.Profile.CopyModifiers(HostVocalProfile);
                    p.RefreshPresets();
                    // Clone before transmitting; remote IDs/cosmetics must never mutate a saved profile.
                    return JsonConvert.DeserializeObject<OnlineProfile>(JsonConvert.SerializeObject(new OnlineProfile
                    {
                        Profile = p.Profile, Engine = p.EnginePreset, RockMeter = p.RockMeterPreset,
                        VoiceStarPower = YARG.Settings.SettingsManager.Settings.VoiceActivatedVocalStarPower.Value
                    }));
                }).ToArray();
                if (IsHost) _speed = speed;
                Send(new OnlineMessage { Type = "ready", Round = _round, Profiles = _localProfiles, Speed = _speed });
                ShowProgress("Ready. Waiting for your friend to choose a difficulty…");
                TryLoadRound();
            }
            catch (Exception e) { Fail(e); }
        }

        private void HandleRound(OnlineMessage m)
        {
            if (m.Round != _round || _round == null) return;
            switch (m.Type)
            {
                case "ready" when _song != null && _remoteProfiles == null && !RoundActive:
                    ValidateProfiles(m.Profiles);
                    _remoteProfiles = m.Profiles;
                    if (!IsHost)
                    {
                        if (!float.IsFinite(m.Speed) || m.Speed < 0.1f || m.Speed > 50)
                            throw new InvalidDataException("Invalid song speed.");
                        _speed = m.Speed;
                        if (!_selecting) BeginSelection();
                    }
                    TryLoadRound();
                    break;
                case "load" when !IsHost && !RoundActive && _localProfiles != null && _remoteProfiles != null:
                    if (!float.IsFinite(m.Speed) || m.Speed < 0.1f || m.Speed > 50)
                        throw new InvalidDataException("Invalid song settings.");
                    _speed = m.Speed;
                    LoadRound();
                    break;
                case "loaded" when RoundActive:
                    _remoteLoaded = true;
                    TryScheduleStart();
                    break;
                case "start" when !IsHost && RoundActive && _localLoaded && !_startScheduled:
                    ValidateSchedule(m);
                    _startScheduled = true;
                    _scheduledControl = m;
                    break;
                case "inputs" when RoundActive && _game != null && _localLoaded:
                    if (m.Index < 0 || m.Index >= _remotePlayers.Count || m.Inputs == null || m.Inputs.Length > 256)
                        throw new InvalidDataException("Invalid performance frame.");
                    var player = _game.Players.First(p => p.Player == _remotePlayers[m.Index]);
                    player.ReceiveOnlineFrame(m.Size, m.SongTime, m.Inputs.Select(i => i.ToInput()).ToArray());
                    break;
                case "finished" when RoundActive && !_remoteFinished:
                    if (m.Results == null || m.Results.Length != _remotePlayers.Count ||
                        m.Results.Any(r => r == null || r.Stats == null || r.Stats.Length > 16384))
                        throw new InvalidDataException("Invalid final results.");
                    _remoteResults = m.Results;
                    _remoteFinished = true;
                    break;
                case "pause-request" when IsHost && _game != null:
                    SchedulePause(m.Flag);
                    break;
                case "pause" when !IsHost && _game != null:
                    ValidateSchedule(m);
                    _scheduledControl = m;
                    break;
                case "restart-request" when IsHost && RoundActive:
                    Restart();
                    break;
                case "restart" when !IsHost && RoundActive:
                    if (!Guid.TryParseExact(m.Data, "N", out _)) throw new InvalidDataException("Invalid round identifier.");
                    _round = m.Data;
                    LoadRound();
                    break;
                case "return-request" when IsHost:
                    ReturnToRoom();
                    break;
                case "return" when !IsHost:
                    ReturnToRoomCore();
                    break;
            }
        }

        private void ValidateProfiles(OnlineProfile[] profiles)
        {
            if (profiles == null || profiles.Length == 0 || profiles.Length > 4)
                throw new InvalidDataException("Invalid player count.");
            foreach (var p in profiles)
            {
                var profile = p?.Profile;
                if (profile == null || p.Engine == null || p.RockMeter == null ||
                    !Enum.IsDefined(typeof(GameMode), profile.GameMode) || !profile.HasValidInstrument ||
                    !Enum.IsDefined(typeof(Difficulty), profile.CurrentDifficulty) || profile.HarmonyIndex > 2 ||
                    !profile.GameMode.PossibleInstrumentsForSong(_song).Contains(profile.CurrentInstrument))
                    throw new InvalidDataException("Your friend's instrument selection is not available in this song.");
                // Keep remote UI data within ordinary local profile limits.
                profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "Friend" : profile.Name.Substring(0, Math.Min(40, profile.Name.Length));
                profile.NoteSpeed = Math.Clamp(profile.NoteSpeed, 0.1f, 30);
                profile.HighwayLength = Math.Clamp(profile.HighwayLength, 0.1f, 5);
            }
        }

        private void TryLoadRound()
        {
            if (!IsHost || _localProfiles == null || _remoteProfiles == null || RoundActive) return;
            if (_localProfiles.Length + _remoteProfiles.Length > 7)
                throw new InvalidOperationException("This room supports at most seven players across both computers.");
            Send(new OnlineMessage { Type = "load", Round = _round, Speed = _speed });
            LoadRound();
        }

        private void LoadRound()
        {
            _game = null;
            _localLoaded = _remoteLoaded = _localFinished = _remoteFinished = _startScheduled = false;
            _scheduledControl = null;
            _sentInputs.Clear();
            _loadStarted = Now;
            _remoteResults = null;
            foreach (var player in _remotePlayers) player.Dispose();
            _remotePlayers.Clear();
            foreach (var wire in _remoteProfiles)
                _remotePlayers.Add(new YargPlayer(wire.Profile, wire.Engine, wire.RockMeter, wire.VoiceStarPower));
            _roundPlayers.Clear();
            _roundPlayers.AddRange(IsHost ? _localPlayers : _remotePlayers);
            _roundPlayers.AddRange(IsHost ? _remotePlayers : _localPlayers);
            RoundActive = true;
            _progress = null;
            DialogManager.Instance.ClearDialog();
            GlobalVariables.State.CurrentSong = _song;
            GlobalVariables.State.CurrentReplay = null;
            GlobalVariables.State.PlayingWithReplay = false;
            GlobalVariables.State.IsPractice = false;
            GlobalVariables.State.PlayingAShow = false;
            GlobalVariables.State.SongSpeed = _speed;
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }

        public async UniTask<bool> WaitForStart(GameManager game)
        {
            int generation = _generation;
            _game = game;
            game.WaitForOnlineStart();
            _localLoaded = true;
            ShowProgress("Waiting for both computers to finish loading…");
            await UniTask.WaitUntil(() => generation != _generation || IsHost || _clock.IsSynchronized);
            if (generation != _generation) return false;
            Send(new OnlineMessage { Type = "loaded", Round = _round });
            TryScheduleStart();
            await UniTask.WaitUntil(() => generation != _generation || _game != game ||
                _scheduledControl?.Type == "start" && HostNow >= _scheduledControl.At);
            if (generation != _generation || _game != game) return false;
            var control = _scheduledControl;
            _scheduledControl = null;
            if (HostNow - control.At > 0.5)
            {
                Fail(new TimeoutException("The start countdown arrived too late. Please try the song again."));
                return false;
            }
            DialogManager.Instance.ClearDialog();
            _progress = null;
            game.StartOnlineSong((HostNow - control.At) * _speed);
            Status = "Playing together";
            return true;
        }

        private void TryScheduleStart()
        {
            if (!IsHost || !_localLoaded || !_remoteLoaded || _startScheduled) return;
            _startScheduled = true;
            _scheduledControl = new OnlineMessage { Type = "start", Round = _round, At = HostNow + 2 };
            Send(_scheduledControl);
        }

        private void ValidateSchedule(OnlineMessage message)
        {
            if (!double.IsFinite(message.At) || message.At < HostNow - 1 || message.At > HostNow + 10 || !_clock.IsSynchronized)
                throw new InvalidDataException("Could not synchronize the room's clock. Please try again.");
        }

        private void UpdateRound()
        {
            if (!RoundActive) return;
            if (!_startScheduled && Now - _loadStarted > 180)
                throw new TimeoutException("The other computer did not finish loading the song.");
            if (_localFinished && !_remoteFinished && Now - _finishedAt > 30)
                throw new TimeoutException("Your friend's final results did not arrive.");
            if (_scheduledControl?.Type == "start")
                ShowProgress($"Starting together in {Math.Max(1, (int) Math.Ceiling(_scheduledControl.At - HostNow))}…");
            if (_game == null || !_game.IsSongStarted) return;
            if (_scheduledControl?.Type == "pause" && !_scheduledControl.Flag)
                ShowProgress($"Resuming together in {Math.Max(1, (int) Math.Ceiling(_scheduledControl.At - HostNow))}…");
            if (_scheduledControl?.Type == "pause" && HostNow >= _scheduledControl.At)
            {
                bool pause = _scheduledControl.Flag;
                _scheduledControl = null;
                DialogManager.Instance.ClearDialog();
                _progress = null;
                _game.ApplyOnlinePause(pause);
            }
        }

        private void LateUpdate()
        {
            if (!RoundActive || _game == null || !_game.IsSongStarted || _localFinished || Now - _lastFrame < 1.0 / 30)
                return;
            try { SendPerformance(); }
            catch (Exception e) { Fail(e); }
        }

        private void SendPerformance()
        {
            _lastFrame = Now;
            for (int i = 0; i < _localPlayers.Count; i++)
            {
                var player = _game.Players.First(p => p.Player == _localPlayers[i]);
                int sent = _sentInputs.GetValueOrDefault(player);
                var inputs = player.ReplayInputs;
                double through = player.BaseEngine.CurrentTime;
                if (through < -3600) continue;
                // Split bursts while keeping the watermark behind any unsent input.
                do
                {
                    int count = Math.Min(256, inputs.Count - sent);
                    var batch = new OnlineInput[count];
                    for (int j = 0; j < count; j++) batch[j] = new OnlineInput(inputs[sent + j]);
                    bool last = sent + count == inputs.Count;
                    Send(new OnlineMessage { Type = "inputs", Round = _round, Index = i, Size = sent,
                        SongTime = last ? through : Math.Min(through, batch[^1].Time), Inputs = batch });
                    sent += count;
                } while (sent < inputs.Count);
                _sentInputs[player] = sent;
            }
        }

        public bool FinishSong()
        {
            if (!_localFinished)
            {
                SendPerformance();
                _localFinished = true;
                _finishedAt = Now;
                var results = _localPlayers.Select(p =>
                {
                    var player = _game.Players.First(b => b.Player == p);
                    return new OnlineFinalScore
                    {
                        Stats = JsonConvert.SerializeObject(player.BaseStats),
                        AverageMultiplier = player.BaseEngine.BaseNoteScore == 0 ? 0 :
                            (float) player.BaseStats.CommittedScore / player.BaseEngine.BaseNoteScore
                    };
                }).ToArray();
                Send(new OnlineMessage { Type = "finished", Round = _round, Results = results });
                Status = "Waiting for your friend's final score…";
            }
            if (!_remoteFinished) return false;
            for (int i = 0; i < _remotePlayers.Count; i++)
            {
                var player = _game.Players.First(p => p.Player == _remotePlayers[i]);
                JsonConvert.PopulateObject(_remoteResults[i].Stats, player.BaseStats,
                    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 16 });
            }
            _game.BandScore = _game.Players.Sum(p => p.Score + p.BandBonusScore);
            _game.EngineManager.UpdateStars();
            return true;
        }

        public float FinalAverageMultiplier(BasePlayer player)
        {
            int index = _remotePlayers.IndexOf(player.Player);
            return index >= 0 ? _remoteResults[index].AverageMultiplier :
                player.BaseEngine.BaseNoteScore == 0 ? 0 :
                (float) player.BaseStats.CommittedScore / player.BaseEngine.BaseNoteScore;
        }

        public bool RequestPause(bool paused)
        {
            if (!RoundActive || _game == null) return false;
            if (IsHost) SchedulePause(paused);
            else Send(new OnlineMessage { Type = "pause-request", Round = _round, Flag = paused });
            return true;
        }

        private void SchedulePause(bool paused)
        {
            if (_scheduledControl != null || _game.Paused == paused) return;
            _scheduledControl = new OnlineMessage { Type = "pause", Round = _round, Flag = paused,
                At = HostNow + (paused ? 0.35 : 1.5) };
            Send(_scheduledControl);
        }

        public void Restart()
        {
            if (!IsHost) { Send(new OnlineMessage { Type = "restart-request", Round = _round }); return; }
            string nextRound = Guid.NewGuid().ToString("N");
            Send(new OnlineMessage { Type = "restart", Round = _round, Data = nextRound });
            _round = nextRound;
            LoadRound();
        }

        public void ReturnToRoom()
        {
            if (!IsHost) { Send(new OnlineMessage { Type = "return-request", Round = _round }); return; }
            Send(new OnlineMessage { Type = "return", Round = _round });
            ReturnToRoomCore();
        }

        private void ReturnToRoomCore(bool menuAlreadyLoaded = false)
        {
            if (_game != null) _game.StopOnlineSong();
            DialogManager.Instance?.ClearDialog();
            _progress = null;
            RoundActive = false;
            ResetTransfer();
            ResetRound();
            _round = null;
            _selecting = false;
            GlobalVariables.State = PersistentState.Default;
            if (!menuAlreadyLoaded) GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            Status = IsHost ? "Friend connected. Choose a song in Quickplay." : "Waiting for the host to choose a song.";
        }

        private void ResetRound()
        {
            _game = null;
            _selecting = false;
            _localProfiles = _remoteProfiles = null;
            _localPlayers.Clear();
            _roundPlayers.Clear();
            foreach (var player in _remotePlayers) player.Dispose();
            _remotePlayers.Clear();
            _sentInputs.Clear();
            _remoteResults = null;
            _scheduledControl = null;
            _localLoaded = _remoteLoaded = _localFinished = _remoteFinished = _startScheduled = false;
        }
    }
}
