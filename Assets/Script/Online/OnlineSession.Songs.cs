using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Helpers;
using YARG.Menu;
using YARG.Menu.Dialogs;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Song;

namespace YARG.Online
{
    public sealed partial class OnlineSession
    {
        private const int CHUNK_BYTES = 12288;
        private const int TRANSFER_WINDOW = 4;
        private string _round;
        private string _offeredHash;
        private SongEntry _song;
        private readonly Dictionary<string, SongEntry> _downloadedSongs = new();
        private bool _selecting;
        private FileStream _upload;
        private FileStream _download;
        private string _packagePath;
        private string _packageDigest;
        private long _packageSize;
        private int _sentChunks;
        private int _ackedChunks;
        private int _receivedChunks;
        private double _transferActivity;
        private bool _preparing;
        private MessageDialog _progress;
        private string CacheDirectory => Path.Combine(PathHelper.PersistentDataPath, "online-songs");

        public bool SelectSong(SongEntry song)
        {
            if (!IsConnected) return false;
            if (!IsHost || !PeerConnected)
            {
                ShowProgress(IsHost ? "Wait for your friend to connect first." : "The host chooses the song.");
                return true;
            }
            if (_round != null)
            {
                ShowProgress("A song is already being prepared. Finish selecting your difficulty, or cancel the song from Online co-op.");
                return true;
            }
            ResetTransfer();
            ResetRound();
            _round = Guid.NewGuid().ToString("N");
            _song = song;
            ShowProgress("Checking your friend's song library…");
            Send(new OnlineMessage { Type = "offer", Round = _round, Hash = song.Hash.ToString(), Text = song.Name });
            return true;
        }

        private void HandleSong(OnlineMessage m)
        {
            if (m.Type == "offer" && !IsHost && !RoundActive)
            {
                if (!Guid.TryParseExact(m.Round, "N", out _) || m.Hash == null || m.Hash.Length > 128 ||
                    m.Hash.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid song offer.");
                ResetTransfer();
                ResetRound();
                _round = m.Round;
                _offeredHash = m.Hash;
                _song = SongContainer.UnfilteredSongs.FirstOrDefault(s => s.Hash.ToString() == m.Hash);
                ShowProgress("Preparing: " + m.Text);
                if (_song == null) _downloadedSongs.TryGetValue(m.Hash, out _song);
                if (_song != null) Send(new OnlineMessage { Type = "song-ready", Round = _round });
                else _ = FindCachedSong(_generation, _round, m.Hash);
                return;
            }
            if (m.Round != _round || _round == null) return;
            switch (m.Type)
            {
                case "need-song" when IsHost && !_preparing && _upload == null && !_selecting:
                    _ = PrepareUpload(_generation, _round);
                    break;
                case "package" when !IsHost && _song == null && _download == null && !_preparing:
                    if (m.Size <= 0 || m.Size > SongPackage.MAX_PACKAGE_BYTES || m.Digest?.Length != 64 ||
                        !m.Digest.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid song package size or checksum.");
                    Directory.CreateDirectory(CacheDirectory);
                    _packagePath = Path.Combine(CacheDirectory, _round + ".download");
                    _packageDigest = m.Digest;
                    _packageSize = m.Size;
                    _download = new FileStream(_packagePath, FileMode.CreateNew, FileAccess.Write);
                    _receivedChunks = 0;
                    _transferActivity = Now;
                    Send(new OnlineMessage { Type = "ack", Round = _round, Index = 0 });
                    break;
                case "chunk" when !IsHost && _download != null:
                    if (m.Index != _receivedChunks || m.Data == null || m.Data.Length > CHUNK_BYTES * 2)
                        throw new InvalidDataException("Song download arrived out of order.");
                    byte[] bytes = Convert.FromBase64String(m.Data);
                    if (bytes.Length == 0 || bytes.Length > CHUNK_BYTES || bytes.Length > _packageSize - _download.Length)
                        throw new InvalidDataException("Invalid song download chunk.");
                    _download.Write(bytes, 0, bytes.Length);
                    _receivedChunks++;
                    _transferActivity = Now;
                    ShowProgress($"Receiving song… {_download.Length * 100 / _packageSize}%");
                    Send(new OnlineMessage { Type = "ack", Round = _round, Index = _receivedChunks });
                    if (_download.Length == _packageSize)
                    {
                        _download.Dispose();
                        _download = null;
                        _ = FinishDownload(_generation, _round);
                    }
                    break;
                case "ack" when IsHost && _upload != null:
                    if (m.Index < _ackedChunks || m.Index > _sentChunks)
                        throw new InvalidDataException("Invalid song download acknowledgement.");
                    _ackedChunks = m.Index;
                    _transferActivity = Now;
                    break;
                case "song-ready" when IsHost && !_selecting:
                    _upload?.Dispose();
                    _upload = null;
                    Send(new OnlineMessage { Type = "select", Round = _round });
                    BeginSelection();
                    break;
                case "select" when !IsHost && _song != null && !_selecting:
                    ShowProgress("The host is choosing instruments. Your selection is next…");
                    break;
            }
        }

        private async Task FindCachedSong(int generation, string round, string hash)
        {
            try
            {
                if (Directory.Exists(CacheDirectory))
                {
                    var cache = await Task.Run(() => CacheHandler.RunScan(false,
                        Path.Combine(CacheDirectory, "songs.bin"), Path.Combine(CacheDirectory, "bad-songs.txt"),
                        false, new List<string> { CacheDirectory }));
                    if (generation != _generation || round != _round) return;
                    foreach (var song in cache.Entries.Values.SelectMany(songs => songs))
                        _downloadedSongs[song.Hash.ToString()] = song;
                    _downloadedSongs.TryGetValue(hash, out _song);
                }
                if (generation != _generation || round != _round) return;
                Send(new OnlineMessage { Type = _song == null ? "need-song" : "song-ready", Round = round });
            }
            catch (Exception e) { if (generation == _generation && round == _round) Fail(e); }
        }

        private async Task PrepareUpload(int generation, string round)
        {
            _preparing = true;
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                string package = Path.Combine(CacheDirectory, round + ".upload");
                string location = _song.ActualLocation;
                ShowProgress("Preparing the song to send…");
                string digest = await Task.Run(() => { SongPackage.Create(location, package); return SongPackage.Digest(package); });
                if (generation != _generation || round != _round) { File.Delete(package); return; }
                _packagePath = package;
                _packageSize = new FileInfo(package).Length;
                if (_packageSize > SongPackage.MAX_PACKAGE_BYTES) throw new InvalidDataException("This song is too large to send.");
                _upload = File.OpenRead(package);
                _transferActivity = Now;
                Send(new OnlineMessage { Type = "package", Round = round, Size = _packageSize, Digest = digest });
            }
            catch (Exception e) { if (generation == _generation && round == _round) Fail(e); }
            finally { if (generation == _generation && round == _round) _preparing = false; }
        }

        private async Task FinishDownload(int generation, string round)
        {
            _preparing = true;
            string package = _packagePath;
            string digest = _packageDigest;
            string folder = Path.Combine(CacheDirectory, round);
            try
            {
                ShowProgress("Checking and loading the downloaded song…");
                var cache = await Task.Run(() =>
                {
                    SongPackage.Extract(package, folder, digest);
                    return CacheHandler.RunScan(false, Path.Combine(folder, "online-cache.bin"),
                        Path.Combine(folder, "bad-songs.txt"), false, new List<string> { folder });
                });
                if (generation != _generation || round != _round) return;
                // The chart has to match the exact offer, not just the displayed title.
                _song = cache.Entries.Values.SelectMany(s => s).SingleOrDefault();
                if (_song == null || _song.Hash.ToString() != _offeredHash) throw new InvalidDataException("The downloaded package did not contain one playable song.");
                _downloadedSongs[_song.Hash.ToString()] = _song;
                Send(new OnlineMessage { Type = "song-ready", Round = round });
            }
            catch (Exception e) { if (generation == _generation && round == _round) Fail(e); }
            finally
            {
                if (File.Exists(package)) File.Delete(package);
                if (generation == _generation && round == _round) _preparing = false;
            }
        }

        private void UpdateTransfer()
        {
            if ((_upload != null || _download != null) && Now - _transferActivity > 60)
                throw new TimeoutException("The song transfer stopped responding.");
            if (_upload == null) return;
            for (int i = 0; i < TRANSFER_WINDOW && _sentChunks - _ackedChunks < TRANSFER_WINDOW && _upload.Position < _upload.Length; i++)
            {
                var bytes = new byte[(int) Math.Min(CHUNK_BYTES, _upload.Length - _upload.Position)];
                int read = _upload.Read(bytes, 0, bytes.Length);
                if (read != bytes.Length) throw new EndOfStreamException();
                Send(new OnlineMessage { Type = "chunk", Round = _round, Index = _sentChunks++, Data = Convert.ToBase64String(bytes) });
            }
            ShowProgress($"Sending song… {Math.Min(_ackedChunks * (long) CHUNK_BYTES, _packageSize) * 100 / _packageSize}%");
        }

        public bool HasSongSelection => _round != null && !RoundActive;

        private void BeginSelection()
        {
            _selecting = true;
            _progress = null;
            DialogManager.Instance.ClearDialog();
            GlobalVariables.State = PersistentState.Default;
            GlobalVariables.State.CurrentSong = _song;
            GlobalVariables.State.ShowSongs.Add(_song);
            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
            Status = "Choose your instrument and difficulty.";
        }

        public void ShowRoomStatus() => ShowProgress(Status);

        private void ShowProgress(string message)
        {
            Status = message;
            if (_progress == null && DialogManager.Instance != null && !DialogManager.Instance.IsDialogShowing)
                _progress = DialogManager.Instance.ShowMessage("Online co-op", message);
            if (_progress != null) _progress.Message.text = message;
        }

        private void ResetTransfer()
        {
            _upload?.Dispose();
            _download?.Dispose();
            _upload = null;
            _download = null;
            // An in-flight task owns its file until it finishes (especially important on Windows).
            if (!_preparing && _packagePath != null && File.Exists(_packagePath)) File.Delete(_packagePath);
            _packagePath = null;
            _preparing = false;
            _sentChunks = _ackedChunks = _receivedChunks = 0;
        }
    }
}
