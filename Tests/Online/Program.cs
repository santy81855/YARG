using System.IO.Compression;
using Newtonsoft.Json;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Online;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Reject(Action action)
{
    try { action(); }
    catch (InvalidDataException) { return; }
    throw new Exception("Invalid peer data was accepted.");
}

var clock = new SessionClock();
Check(clock.Observe(10, 10.12, 45.05, 45.07), "Clock sample rejected");
Check(Math.Abs(clock.Offset - 35) < 1e-10, "Host clock estimate incorrect");
Check(Math.Abs(clock.RoundTripSeconds - .1) < 1e-10, "Server work counted as network time");
Check(!clock.Observe(11, 11.4, 46.2, 46.2), "Slower sample replaced minimum RTT");
Check(!clock.Observe(double.NaN, 20, 30, 30), "NaN clock accepted");
Check(!clock.Observe(20, 19, 30, 30), "Backwards clock accepted");
Check(clock.Observe(50, 50.3, 85.15, 85.15), "Clock sample window did not expire");
Console.WriteLine("PASS: independent clock epochs, server processing time, jitter rejection, drift window, invalid samples");

string root = Path.Combine(Path.GetTempPath(), "yarg-online-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string source = Path.Combine(root, "source");
    Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source, "song.ini"), "[song]\nname = Test\n");
    byte[] audio = new byte[200000]; new Random(123).NextBytes(audio);
    File.WriteAllBytes(Path.Combine(source, "song.ogg"), audio);
    File.WriteAllText(Path.Combine(source, "notes.chart"), "Test chart");
    File.WriteAllText(Path.Combine(source, "unrelated.exe"), "never share");
    string package = Path.Combine(root, "song.zip");
    SongPackage.Create(source, package);
    string digest = SongPackage.Digest(package);
    string destination = Path.Combine(root, "guest");
    SongPackage.Extract(package, destination, digest);
    Check(File.ReadAllBytes(Path.Combine(destination, "song.ogg")).SequenceEqual(audio), "Audio transfer changed bytes");
    Check(Directory.GetFiles(destination).Length == 3, "Unrelated files were transferred");
    Reject(() => SongPackage.Extract(package, Path.Combine(root, "bad-hash"), new string('0', 64)));
    Check(!Directory.Exists(Path.Combine(root, "bad-hash")), "Extraction occurred before checksum validation");
    foreach (string bad in new[] { "../escape.ogg", "sub/song.ini", "..\\escape.ogg", "C:escape.ogg", "NUL.ogg", "song.exe" })
    {
        string attack = Path.Combine(root, Guid.NewGuid() + ".zip");
        using (var zip = ZipFile.Open(attack, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry(bad).Open());
            writer.Write("bad");
        }
        Reject(() => SongPackage.Extract(attack, Path.Combine(root, "rejected"), SongPackage.Digest(attack)));
        Check(!Directory.Exists(Path.Combine(root, "rejected")), "Invalid package partially extracted");
    }
    string duplicate = Path.Combine(root, "duplicate.zip");
    using (var zip = ZipFile.Open(duplicate, ZipArchiveMode.Create))
    { zip.CreateEntry("song.ogg"); zip.CreateEntry("SONG.ogg"); }
    Reject(() => SongPackage.Extract(duplicate, Path.Combine(root, "duplicates"), SongPackage.Digest(duplicate)));
    string sng = Path.Combine(root, "one.sng"); File.WriteAllBytes(sng, audio);
    string sngPackage = Path.Combine(root, "sng.zip"); SongPackage.Create(sng, sngPackage);
    SongPackage.Extract(sngPackage, Path.Combine(root, "sng"), SongPackage.Digest(sngPackage));
    Check(File.ReadAllBytes(Path.Combine(root, "sng", "one.sng")).SequenceEqual(audio), "SNG transfer failed");
    Console.WriteLine("PASS: folder/SNG round trips, original audio, checksums, path traversal, Windows filenames, duplicate names");
}
finally { Directory.Delete(root, true); }

static YargVocalsEngine Engine()
{
    var sync = new SyncTrack(480);
    sync.Tempos.Add(new TempoChange(120, 0, 0));
    sync.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0, 0, 0, 0, 0, 0));
    var phrase = new VocalNote(NoteFlags.None, false, 0, 2, 0, 1920);
    var note = new VocalNote(60, 0, VocalNoteType.Lyric, .1, 1.6, 96, 1536);
    phrase.AddChildNote(note);
    var chart = new InstrumentDifficulty<VocalNote>(Instrument.Vocals, Difficulty.Expert, new() { phrase }, new(), new());
    float[] thresholds = { .05f, .11f, .19f, .46f, .77f, 1.06f };
    var parameters = new VocalsEngineParameters(new HitWindowSettings(.1, .1, 1, false, 0, 1, 1, 0, 0),
        4, thresholds, thresholds, 1.5f, .5f, .75, 100, false, 1000);
    return new YargVocalsEngine(chart, sync, parameters, false);
}

foreach (int key in new[] { -12, -5, 0, 4, 12 })
{
    var local = Engine();
    var remote = Engine();
    var recording = new List<GameInput>();
    var stream = new RemoteInputStream(recording);
    var pending = new List<GameInput>();
    int sent = 0;
    for (int i = 1; i <= 210; i++)
    {
        double time = i / 100.0;
        int currentKey = time < 1 ? key : -key;
        float sung = SongKey.ToPlaybackPitch(60, currentKey);
        var input = SongKey.ToChartInput(GameInput.Create(time, VocalsAction.Pitch, sung), currentKey);
        local.QueueInput(ref input);
        local.Update(time);
        pending.Add(input);
        // Variable packet delay/batching, including more than a whole vocal note.
        if (i % 73 == 0 || i == 210)
        {
            stream.Append(sent, time, pending.ToArray(), 3);
            sent += pending.Count; pending.Clear();
            while (stream.TryDequeue(out var received)) remote.QueueInput(ref received);
            remote.Update(stream.ThroughTime);
        }
    }
    Check(local.EngineStats.TotalScore > 0, "Test singer did not score");
    Check(remote.EngineStats.TotalScore == local.EngineStats.TotalScore &&
        remote.EngineStats.TicksHit == local.EngineStats.TicksHit && remote.EngineStats.TicksMissed == local.EngineStats.TicksMissed,
        "Packet delay or independent key changed remote scoring");
    Check(recording.Count == 210, "Remote input was lost or duplicated");
    Reject(() => stream.Append(209, 2.1, Array.Empty<GameInput>(), 3));
    Reject(() => stream.Append(210, 2, Array.Empty<GameInput>(), 3));
    Reject(() => stream.Append(210, double.NaN, Array.Empty<GameInput>(), 3));
    Check(stream.ThroughTime == 2.1, "Rejected frame changed the watermark");
    var final = new VocalsStats();
    JsonConvert.PopulateObject(JsonConvert.SerializeObject(local.EngineStats), final);
    Check(final.TotalScore == local.EngineStats.TotalScore && final.TicksHit == local.EngineStats.TicksHit,
        "Final score exchange lost vocal results");
}
Console.WriteLine("PASS: actual vocals engine, packet stalls, independent keys, mid-song changes, ordered delivery, final score exchange");

var future = new RemoteInputStream(new List<GameInput>());
future.Append(0, 0.5, new[] { new GameInput(1, 0, 60f) }, 3);
Check(!future.TryDequeue(out _), "Future input scored before its completed frame");
future.Append(1, 1, Array.Empty<GameInput>(), 3);
Check(future.TryDequeue(out var next) && next.Time == 1 && !future.TryDequeue(out _), "Future input was lost or consumed twice");
Console.WriteLine("PASS: input watermark preserves future events without advancing the remote engine prematurely");
