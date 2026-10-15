using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Logging;
using YARG.Menu.Persistent;

namespace YARG.Online
{
    [DefaultExecutionOrder(10000)]
    public sealed partial class OnlineSession : MonoBehaviour
    {
        private const string CHANNEL = "YARG.CoOp.1";
        private const int PROTOCOL = 1;
        private const int MAX_MESSAGE = 256 * 1024;
        private static OnlineSession _instance;

        public static OnlineSession Instance
        {
            get
            {
                if (_instance == null)
                {
                    var root = new GameObject("Online co-op");
                    DontDestroyOnLoad(root);
                    _instance = root.AddComponent<OnlineSession>();
                }
                return _instance;
            }
        }

        public static OnlineSession Current => _instance;
        public bool IsConnected => _network != null && _network.IsListening;
        public bool IsHost => _network != null && _network.IsHost;
        public bool PeerConnected { get; private set; }
        public string JoinCode { get; private set; }
        public string Status { get; private set; } = "Create a room or enter your friend's code.";
        public bool RoundActive { get; private set; }
        public IReadOnlyList<YARG.Player.YargPlayer> RoundPlayers => _roundPlayers;

        private NetworkManager _network;
        private ulong _peer;
        private bool _connecting;
        private int _generation;
        private double _lastPacket;
        private double _lastPing;
        private SessionClock _clock = new();
        private double Now => Time.realtimeSinceStartupAsDouble;
        private double HostNow => Now + (IsHost ? 0 : _clock.Offset);
        private string Compatibility => PROTOCOL + ":" + Application.version;

        public async Task Connect(bool host, string code = null, bool loopback = false)
        {
            if (_connecting || IsConnected) return;
            _connecting = true;
            int generation = ++_generation;
            try
            {
                Status = host ? "Creating room…" : "Connecting…";
                if (!loopback)
                {
                    if (string.IsNullOrEmpty(Application.cloudProjectId))
                        throw new InvalidOperationException("Link this project in Edit > Project Settings > Services, " +
                            "then enable Relay in that Unity project's Dashboard. Both builds must use that project.");
                    await UnityServices.InitializeAsync();
                    if (!AuthenticationService.Instance.IsSignedIn)
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                if (generation != _generation) return;

                var root = new GameObject("Co-op transport");
                DontDestroyOnLoad(root);
                var transport = root.AddComponent<UnityTransport>();
                _network = root.AddComponent<NetworkManager>();
                _network.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    EnableSceneManagement = false,
                    ConnectionApproval = true,
                    ConnectionData = Encoding.UTF8.GetBytes(Compatibility),
                    ClientConnectionBufferTimeout = 15,
                    TickRate = 60
                };
                _network.ConnectionApprovalCallback = (request, response) =>
                {
                    response.Approved = _network.ConnectedClientsIds.Count < 2 &&
                        Encoding.UTF8.GetString(request.Payload) == Compatibility;
                    response.CreatePlayerObject = false;
                    response.Reason = "The room is full or the game builds do not match.";
                };
                _network.OnClientConnectedCallback += OnConnected;
                _network.OnClientDisconnectCallback += OnDisconnected;

                if (loopback)
                {
                    // Development test only; the normal menu always uses Relay.
                    transport.SetConnectionData("127.0.0.1", 27941);
                    JoinCode = "LOCAL";
                }
                else if (host)
                {
                    var allocation = await RelayService.Instance.CreateAllocationAsync(1);
                    if (generation != _generation) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                    JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                }
                else
                {
                    var allocation = await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                    if (generation != _generation) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                    JoinCode = code.Trim().ToUpperInvariant();
                }
                if (generation != _generation) return;
                bool started = host ? _network.StartHost() : _network.StartClient();
                if (!started) throw new InvalidOperationException("The network transport could not start.");
                _network.CustomMessagingManager.RegisterNamedMessageHandler(CHANNEL, OnMessage);
                _lastPacket = Now;
                Status = host ? "Waiting for your friend. Share code: " + JoinCode : "Joining room…";
            }
            catch (Exception e)
            {
                if (generation == _generation) Fail(e);
            }
            finally
            {
                if (generation == _generation) _connecting = false;
            }
        }

        private void OnConnected(ulong client)
        {
            if (_network == null || client == NetworkManager.ServerClientId && IsHost) return;
            _peer = IsHost ? client : NetworkManager.ServerClientId;
            // The application handshake also catches protocol mismatches on local test transports.
            Send(new OnlineMessage { Type = "hello", Text = Compatibility });
        }

        private void OnDisconnected(ulong client)
        {
            if (_network == null || IsHost && client == NetworkManager.ServerClientId) return;
            string reason = _network.DisconnectReason;
            Fail(new InvalidOperationException(string.IsNullOrEmpty(reason) ? "Your friend disconnected." : reason));
        }

        private void Update()
        {
            if (!IsConnected) return;
            try
            {
                if (Now - _lastPing > 0.5)
                {
                    _lastPing = Now;
                    if (PeerConnected)
                        Send(new OnlineMessage { Type = IsHost ? "heartbeat" : "ping", Sent = Now });
                }
                if (PeerConnected && Now - _lastPacket > 25 || !IsHost && !PeerConnected && Now - _lastPacket > 30)
                    throw new TimeoutException("The other computer stopped responding.");
                UpdateTransfer();
                UpdateRound();
            }
            catch (Exception e) { Fail(e); }
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (_network == null || sender != _peer || reader.Length > MAX_MESSAGE) return;
            try
            {
                reader.ReadValueSafe(out string json);
                var message = JsonConvert.DeserializeObject<OnlineMessage>(json,
                    new JsonSerializerSettings { MaxDepth = 32, TypeNameHandling = TypeNameHandling.None });
                if (message == null) throw new InvalidOperationException("Empty room message.");
                _lastPacket = Now;
                if (message.Type == "hello")
                {
                    if (message.Text != Compatibility) throw new InvalidOperationException("Both players need the same game build.");
                    bool first = !PeerConnected;
                    PeerConnected = true;
                    Status = IsHost ? "Friend connected. Choose a song in Quickplay." : "Connected. Waiting for the host to choose a song.";
                    if (first) Send(new OnlineMessage { Type = "hello", Text = Compatibility });
                    return;
                }
                if (!PeerConnected) return;
                if (message.Type == "ping" && IsHost)
                {
                    Send(new OnlineMessage { Type = "pong", Sent = message.Sent, Received = Now, At = Now });
                    return;
                }
                if (message.Type == "pong" && !IsHost)
                {
                    _clock.Observe(message.Sent, Now, message.Received, message.At);
                    return;
                }
                if (message.Type == "heartbeat") return;
                HandleSong(message);
                HandleRound(message);
            }
            catch (Exception e) { Fail(e); }
        }

        private void Send(OnlineMessage message)
        {
            if (_network == null || !_network.IsListening) return;
            string json = JsonConvert.SerializeObject(message, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Ignore
            });
            int bytes = Encoding.UTF8.GetByteCount(json);
            if (bytes > MAX_MESSAGE / 2) throw new InvalidOperationException("Room message exceeded its size limit.");
            using var writer = new FastBufferWriter(json.Length * 2 + 16, Allocator.Temp);
            writer.WriteValueSafe(json);
            _network.CustomMessagingManager.SendNamedMessage(CHANNEL, _peer, writer,
                NetworkDelivery.ReliableFragmentedSequenced);
        }

        public void Leave()
        {
            if (_game != null) _game.StopOnlineSong();
            ++_generation;
            _connecting = false;
            PeerConnected = false;
            RoundActive = false;
            JoinCode = null;
            _clock = new SessionClock();
            ResetTransfer();
            ResetRound();
            if (_network != null)
            {
                var network = _network;
                _network = null;
                network.OnClientConnectedCallback -= OnConnected;
                network.OnClientDisconnectCallback -= OnDisconnected;
                network.Shutdown();
                Destroy(network.gameObject);
            }
            Status = "Disconnected.";
        }

        private void Fail(Exception exception)
        {
            YargLogger.LogException(exception, "Online co-op");
            bool wasPlaying = RoundActive;
            Leave();
            Status = exception.Message;
            if (wasPlaying)
            {
                GlobalVariables.State = PersistentState.Default;
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            }
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.ClearDialog();
                DialogManager.Instance.ShowMessage("Online co-op", Status);
            }
        }

        private void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            // A gameplay load failure must also release the other computer's loading barrier.
            if (!RoundActive || scene.buildIndex != (int) SceneIndex.Menu) return;
            Send(new OnlineMessage { Type = IsHost ? "return" : "return-request", Round = _round });
            ReturnToRoomCore(menuAlreadyLoaded: true);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Leave();
            if (_instance == this) _instance = null;
        }

        private void OnApplicationQuit() => Leave();
    }
}
