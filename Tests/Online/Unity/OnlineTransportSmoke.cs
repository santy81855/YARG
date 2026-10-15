#if UNITY_5_3_OR_NEWER
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace YARG.Online.Validation
{
    // Copy this file into Assets/OnlineValidation in a disposable project snapshot.
    // It is deliberately outside Assets in the normal game.
    public sealed class OnlineTransportSmoke : MonoBehaviour
    {
        private const string CHANNEL = "YARG.Smoke";
        private int _received;
        private int _lastReceived = -1;

        [RuntimeInitializeOnLoadMethod]
        private static void Boot()
        {
            if (Environment.GetCommandLineArgs().Contains("-online-smoke"))
                new GameObject("Transport validation").AddComponent<OnlineTransportSmoke>();
        }

        private async void Start()
        {
            try
            {
                Application.runInBackground = true;
                bool host = Environment.GetCommandLineArgs().Contains("-host");
                var session = OnlineSession.Instance;
                await session.Connect(host, loopback: true);
                double deadline = Time.realtimeSinceStartupAsDouble + 45;
                await Until(() => session.PeerConnected, deadline);
                var network = (NetworkManager) typeof(OnlineSession).GetField("_network",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                var clock = (SessionClock) typeof(OnlineSession).GetField("_clock",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                if (!host) await Until(() => clock.IsSynchronized, deadline);
                ulong peer = host ? network.ConnectedClientsIds.First(id => id != 0) : 0;
                network.CustomMessagingManager.RegisterNamedMessageHandler(CHANNEL, (sender, reader) =>
                {
                    reader.ReadValueSafe(out string message);
                    int index = int.Parse(message.Substring(0, message.IndexOf(':')));
                    string payload = message.Substring(message.IndexOf(':') + 1);
                    if (index != _lastReceived + 1 || payload != new string((char) ('A' + index), 20000))
                        throw new InvalidOperationException("Fragmented payload was altered or reordered.");
                    _lastReceived = index;
                    _received++;
                });
                // Allow both named handlers to be registered before sending.
                await Task.Delay(1000);
                for (int index = 0; index < 20; index++)
                {
                    string payload = index + ":" + new string((char) ('A' + index), 20000);
                    using var writer = new FastBufferWriter(payload.Length * 2 + 16, Allocator.Temp);
                    writer.WriteValueSafe(payload);
                    network.CustomMessagingManager.SendNamedMessage(CHANNEL, peer, writer,
                        NetworkDelivery.ReliableFragmentedSequenced);
                    await Task.Delay(50);
                }
                await Until(() => _received == 20, deadline);
                Debug.Log("ONLINE_SMOKE_PASS: " + (host ? "host" : "guest") +
                    ", handshake, clock, 20 ordered fragmented messages each way");
                // Keep the connection alive until both endpoints have finished their assertions.
                await Task.Delay(2000);
                Application.Quit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("ONLINE_SMOKE_FAIL: " + e);
                Application.Quit(1);
            }
        }

        private static async Task Until(Func<bool> predicate, double deadline)
        {
            while (!predicate())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException();
                await Task.Delay(20);
            }
        }

#if UNITY_EDITOR
        public static void CheckMenu()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Menu/MainMenu/MainMenu.prefab");
            var instance = Instantiate(prefab);
            try
            {
                var menu = instance.GetComponent<YARG.Menu.Main.MainMenu>();
                typeof(YARG.Menu.Main.MainMenu).GetMethod("AddOnlineButton",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
                var online = instance.GetComponentsInChildren<YARG.Menu.Navigation.NavigatableButton>(true)
                    .Single(button => button.name == "Online co-op");
                if (online.transform.GetSiblingIndex() != 1 ||
                    online.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text != "Online co-op")
                    throw new InvalidOperationException("Online menu entry is missing or misplaced.");
                var click = (UnityEngine.UI.Button.ButtonClickedEvent) typeof(YARG.Menu.Navigation.NavigatableButton)
                    .GetField("_onClick", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(online);
                if (click.GetPersistentEventCount() != 0)
                    throw new InvalidOperationException("Cloned button retained its Quickplay action.");
                Debug.Log("ONLINE_MENU_PASS: native menu button below Quickplay, correct label, no inherited action");
            }
            finally { DestroyImmediate(instance); }
            CheckSongKeyRow();
        }

        private static void CheckSongKeyRow()
        {
            var card = UnityEditor.AssetDatabase.LoadAssetAtPath<YARG.Menu.DifficultySelect.DifficultyItem>(
                "Assets/Prefabs/Menu/DifficultySelect/DifficultyItem.prefab");
            var button = UnityEditor.AssetDatabase.LoadAssetAtPath<YARG.Menu.ColoredButton>(
                "Assets/Prefabs/Menu/Common/Buttons/SmallRoundButton.prefab");
            var parent = new GameObject("Key control validation", typeof(RectTransform));
            var menuData = parent.AddComponent<YARG.Menu.Data.MenuData>();
            typeof(YARG.Menu.Data.MenuData).GetField("_colors", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(menuData, UnityEditor.AssetDatabase.LoadAssetAtPath<YARG.Menu.Data.MenuColors>(
                    "Assets/Art/Menu/Data/MenuColors.asset"));
            var singleton = typeof(MonoSingleton<YARG.Menu.Data.MenuData>).GetProperty("Instance");
            singleton.SetValue(null, menuData);
            try
            {
                int key = 0;
                var row = YARG.Menu.DifficultySelect.SongKeySelectItem.Create(card, button, parent.transform,
                    () => key, value => key = value, () => "Highest: " + YARG.Helpers.VocalPitchPreview.NoteName(69 + key));
                var buttons = row.GetComponentsInChildren<YARG.Menu.ColoredButton>();
                var minus = buttons.Single(b => b.name == "Lower key");
                var plus = buttons.Single(b => b.name == "Raise key");
                minus.OnClick.Invoke();
                if (key != -1 || !row.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text == "Highest: G#4"))
                    throw new InvalidOperationException("Minus button did not update key and highest note.");
                plus.OnClick.Invoke();
                if (key != 0) throw new InvalidOperationException("Plus button did not raise the key.");
                key = 12; row.Refresh();
                if (plus.GetComponentInChildren<UnityEngine.UI.Button>().interactable)
                    throw new InvalidOperationException("Upper key bound is not reflected in the button.");
                key = -12; row.Refresh();
                if (minus.GetComponentInChildren<UnityEngine.UI.Button>().interactable)
                    throw new InvalidOperationException("Lower key bound is not reflected in the button.");
                Debug.Log("SONG_KEY_MENU_PASS: native cards/buttons, pitch context, minus/plus, bounds");
            }
            finally { singleton.SetValue(null, null); DestroyImmediate(parent); }
        }

        public static void Build()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/OnlineValidation/Smoke.unity");
            var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
            {
                scenes = new[] { "Assets/OnlineValidation/Smoke.unity" },
                locationPathName = "/tmp/yarg-online-smoke/Smoke.x86_64",
                target = UnityEditor.BuildTarget.StandaloneLinux64,
                options = UnityEditor.BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Transport test build failed.");
        }
#endif
    }
}
#endif
