using ManagedBass;
using ManagedBass.Fx;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;
using YARG.Gameplay;
using YARG.Helpers;
using YARG.Settings;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// The remembered key is isolated by song and never leaks into a replay.
var manager = new GameManager();
Check(manager.SetSongKey(-3), "Key change failed");
Check(manager.SongKeySemitones == -3 && manager.Mixer.Key == -3, "Audio and targets disagree");
Check(manager.Mixer.Seeks == 1 && manager.VocalTrack.Refreshes == 1, "Buffers or guides were not refreshed");
Check(SettingsManager.Saves == 1, "Key change did not save settings");
SettingsManager.Settings = Newtonsoft.Json.JsonConvert.DeserializeObject<SettingsManager.SettingContainer>(
    SettingsManager.LastSavedJson);
var anotherSession = new GameManager();
anotherSession.LoadKey();
Check(anotherSession.SongKeySemitones == -3, "Song key was not restored");
// Each gameplay scene creates its own manager.
var secondSong = new GameManager { Song = new FakeSong { Hash = "song-two" } };
secondSong.LoadKey();
Check(secondSong.SongKeySemitones == 0, "Key leaked to another song");
Check(manager.SetSongKey(100) && manager.SongKeySemitones == 12, "Upper limit failed");
Check(manager.SetSongKey(-100) && manager.SongKeySemitones == -12, "Lower limit failed");
manager.Mixer.Succeed = false;
Check(!manager.SetSongKey(4) && manager.SongKeySemitones == -12, "Audio failure changed targets");
manager.Mixer.Succeed = true;
Check(manager.SetSongKey(0) && !SettingsManager.Settings.SongKeyOffsets.ContainsKey("song-one"), "Reset was not saved");
manager.Paused = false;
Check(!manager.SetSongKey(2), "Key changed while playing");
var replay = new GameManager { ReplayInfo = new object() };
SettingsManager.Settings.SongKeyOffsets["song-one"] = 5;
replay.LoadKey();
Check(replay.SongKeySemitones == 0 && !replay.SetSongKey(3), "Replay used remembered key");
SettingsManager.FailSave = true;
manager.Paused = true;
Check(manager.SetSongKey(2) && manager.SongKeySemitones == 2, "Save failure broke playback");
Check(YARG.Menu.Persistent.ToastManager.LastError == "Menu.SongKey.SaveError", "Save failure was hidden");
SettingsManager.FailSave = false;
Console.WriteLine("PASS: per-song persistence, reset, bounds, replay isolation, and failure handling");

static (uint Hit, uint Miss, long Score) Sing(int semitones, int part, bool talkie = false, bool wrongKey = false, bool changeKey = false)
{
    var sync = new SyncTrack(480);
    sync.Tempos.Add(new TempoChange(120, 0, 0));
    sync.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0, 0, 0, 0, 0, 0));
    var phrase = new VocalNote(NoteFlags.None, false, 0, 2, 0, 1920);
    var note = new VocalNote(talkie ? -1 : 60 + part * 4, part, VocalNoteType.Lyric, .1, .6, 96, 576);
    if (!talkie) note.AddChildNote(new VocalNote(64 + part * 4, part, VocalNoteType.Lyric, 1.0, .8, 960, 768));
    phrase.AddChildNote(note);
    var chart = new InstrumentDifficulty<VocalNote>(part == 0 ? Instrument.Vocals : Instrument.Harmony,
        Difficulty.Expert, new() { phrase }, new(), new());
    float[] thresholds = { .05f, .11f, .19f, .46f, .77f, 1.06f };
    var parameters = new VocalsEngineParameters(new HitWindowSettings(.1, .1, 1, false, 0, 1, 1, 0, 0),
        4, thresholds, thresholds, 1.5f, .5f, .75, 100, false, 1000);
    var engine = new YargVocalsEngine(chart, sync, parameters, false);
    for (int i = 1; i <= 210; i++)
    {
        double time = i / 100.0;
        int key = changeKey && time >= 1 ? -semitones : semitones;
        float pitch = talkie ? 70 : note.PitchAtSongTime(time);
        float sung = wrongKey ? pitch : SongKey.ToPlaybackPitch(pitch, key);
        var input = SongKey.ToChartInput(GameInput.Create(time, VocalsAction.Pitch, sung), key);
        Check(input.Time == time, "Input time changed");
        engine.QueueInput(ref input);
        engine.Update(time);
    }
    return (engine.EngineStats.TicksHit, engine.EngineStats.TicksMissed, engine.EngineStats.TotalScore);
}

for (int part = 0; part < 3; part++)
{
    var baseline = Sing(0, part);
    Check(baseline.Hit > 0 && baseline.Score > 0, "Baseline singing did not score");
    for (int key = -12; key <= 12; key++)
    {
        Check(Sing(key, part) == baseline, $"Scoring differs in key {key}, harmony {part}");
        Check(Sing(key, part, changeKey: true) == baseline, "Mid-phrase key change broke scoring");
    }
    Check(Sing(3, part, wrongKey: true).Hit < baseline.Hit, "Original-key singing matched transposed targets");
}
Check(Sing(-7, 0, talkie: true) == Sing(0, 0, talkie: true), "Talkie scoring changed");
foreach (var action in new[] { VocalsAction.Hit, VocalsAction.StarPower })
{
    var input = GameInput.Create(1.25, action, true);
    var converted = SongKey.ToChartInput(input, -5);
    Check(converted.Time == input.Time && converted.Action == input.Action && converted.Integer == input.Integer,
        "Non-pitched input changed");
}
Console.WriteLine("PASS: actual vocals engine, all 25 keys, solo/harmonies, slides, mid-phrase changes, talkies, and percussion inputs");

// Decode a synthetic A4 through the shipped Linux BASS/Fx libraries. This needs
// no sound device and checks measured pitch and duration, rather than attributes alone.
Check(Bass.Init(0, 44100), $"BASS init: {Bass.LastError}");
string wavePath = Path.Combine(Path.GetTempPath(), $"yarg-key-{Guid.NewGuid():N}.wav");
try
{
    const int rate = 44100;
    const int frames = rate * 4;
    using (var writer = new BinaryWriter(File.Create(wavePath)))
    {
        writer.Write("RIFF"u8); writer.Write(36 + frames * 2); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short) 1); writer.Write((short) 1);
        writer.Write(rate); writer.Write(rate * 2); writer.Write((short) 2); writer.Write((short) 16);
        writer.Write("data"u8); writer.Write(frames * 2);
        for (int i = 0; i < frames; i++) writer.Write((short) (12000 * Math.Sin(2 * Math.PI * 440 * i / rate)));
    }
    foreach (var (key, speed, chipmunk) in new[]
    {
        (-12, 1f, false), (-3, 1f, false), (0, 1f, false), (3, 1f, false), (12, 1f, false),
        (-3, .75f, false), (3, 1.25f, false), (-3, .75f, true), (3, 1.25f, true)
    })
    {
        int source = Bass.CreateStream(wavePath, 0, 0, BassFlags.Decode | BassFlags.Float);
        Check(source != 0, $"Create source: {Bass.LastError}");
        int tempo = BassFx.TempoCreate(source, BassFlags.Decode);
        Check(tempo != 0, $"Create tempo: {Bass.LastError}");
        try
        {
            Check(Bass.ChannelSetAttribute(tempo, ChannelAttribute.Tempo, (speed - 1) * 100), "Set tempo");
            Check(Bass.ChannelSetAttribute(tempo, ChannelAttribute.Pitch, SongKey.AudioPitch(key, speed, chipmunk)), "Set key");
            var samples = new List<float>();
            var buffer = new float[4096];
            int bytes;
            while ((bytes = Bass.ChannelGetData(tempo, buffer, buffer.Length * 4)) > 0)
                samples.AddRange(buffer.Take(bytes / 4));
            double duration = samples.Count / (double) rate;
            Check(Math.Abs(duration - 4 / speed) < .05, $"Key {key} changed duration: {duration}");
            var crossings = new List<double>();
            for (int i = rate; i < samples.Count - rate; i++)
            {
                if (samples[i - 1] <= 0 && samples[i] > 0)
                    crossings.Add(i - 1 + -samples[i - 1] / (samples[i] - samples[i - 1]));
            }
            double measured = (crossings.Count - 1) * rate / (crossings[^1] - crossings[0]);
            double expected = 440 * Math.Pow(2, key / 12.0) * (chipmunk ? speed : 1);
            Check(Math.Abs(measured - expected) < 2, $"Key {key}: expected {expected:F2}Hz, got {measured:F2}Hz");
            Console.WriteLine($"PASS: Linux audio {key:+0;-0;0} semitones, speed {speed}, chipmunk {chipmunk}: {measured:F2}Hz, {duration:F3}s");
        }
        finally
        {
            Bass.StreamFree(tempo);
            Bass.StreamFree(source);
        }
    }
}
finally
{
    File.Delete(wavePath);
    Bass.Free();
}

// Pre-song range context uses chart pitches, including the end of a slide.
var rangePhrase = new VocalNote(NoteFlags.None, false, 0, 4, 0, 3840);
var slide = new VocalNote(60, 0, VocalNoteType.Lyric, .1, 1, 96, 960);
slide.AddChildNote(new VocalNote(78, 0, VocalNoteType.Lyric, 1.1, 1, 1056, 960));
rangePhrase.AddChildNote(slide);
rangePhrase.AddChildNote(new VocalNote(127, 0, VocalNoteType.Percussion, 3, .1, 2880, 96));
rangePhrase.AddChildNote(new VocalNote(-1, 0, VocalNoteType.Lyric, 3.5, .1, 3360, 96));
var soloPart = new VocalsPart(false,
    new() { new VocalsPhrase(0, 4, 0, 3840, rangePhrase, new()) }, new(), new(), new());
Check(VocalPitchPreview.HighestPitch(soloPart) == 78, "Slide endpoint or percussion range is wrong");
Check(VocalPitchPreview.NoteName(60) == "C4" && VocalPitchPreview.NoteName(69) == "A4" &&
    VocalPitchPreview.NoteName(66) == "F#4", "Vocal note names use the wrong octave or pitch class");
Check(VocalPitchPreview.NoteName(78 - 5) == "C#5", "Highest note did not transpose down");
Check(VocalPitchPreview.NoteName(78 + 6) == "C6", "Highest note did not cross octave boundary");
var chartPreview = new SongChart(480);
chartPreview.Vocals.Parts.Add(soloPart);
var harmonyPhrase = new VocalNote(NoteFlags.None, false, 0, 2, 0, 1920);
harmonyPhrase.AddChildNote(new VocalNote(69, 1, VocalNoteType.Lyric, .1, 1, 96, 960));
chartPreview.Harmony.Parts.Add(new VocalsPart(true, new(), new(), new(), new()));
chartPreview.Harmony.Parts.Add(new VocalsPart(true,
    new() { new VocalsPhrase(0, 2, 0, 1920, harmonyPhrase, new()) }, new(), new(), new()));
var pitches = new VocalPitchPreview(chartPreview);
Check(pitches.Highest(Instrument.Vocals, 0) == 78 && pitches.Highest(Instrument.Harmony, 1) == 69,
    "Range did not follow the selected vocal part");
Check(pitches.Highest(Instrument.Harmony, 0) == null && pitches.Highest(Instrument.Harmony, 2) == null,
    "Empty or absent part invented a vocal note");
Check(pitches.HighestAcrossParts() == 78, "Shared setup did not include the solo vocal part");
harmonyPhrase.AddChildNote(new VocalNote(90, 1, VocalNoteType.Lyric, 1.2, .5, 1152, 480));
Check(new VocalPitchPreview(chartPreview).HighestAcrossParts() == 90,
    "Shared setup did not include a higher harmony part");
Check(new VocalPitchPreview(new SongChart(480)).HighestAcrossParts() == null,
    "Shared setup invented a highest note in a chart without vocals");
Console.WriteLine("PASS: pre-song highest note, slide endpoints, percussion/talkie exclusion, note names, transposition, separate harmony parts");
