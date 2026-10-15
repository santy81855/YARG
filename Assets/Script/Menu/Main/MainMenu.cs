using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;
using YARG.Settings;

namespace YARG.Menu.Main
{
    public class MainMenu : MonoBehaviour
    {
        private static bool _antiPiracyDialogShown;

        [SerializeField]
        private TextMeshProUGUI _versionText;

        private void Start()
        {
            AddOnlineButton();
            _versionText.text = GlobalVariables.Instance.CurrentVersion;

            // Show the anti-piracy dialog if it hasn't been shown already
            // Also only show it once per game launch
            if (!_antiPiracyDialogShown && SettingsManager.Settings.ShowAntiPiracyDialog)
            {
                DialogManager.Instance.ShowOneTimeMessage(
                    "Menu.Dialog.AntiPiracy",
                    () =>
                    {
                        SettingsManager.Settings.ShowAntiPiracyDialog = false;
                        SettingsManager.SaveSettings();
                    });

                _antiPiracyDialogShown = true;
            }

            if (SettingsMenu.ConsumeOpenOnNextMenuLoad())
            {
                SettingsMenu.Instance.gameObject.SetActive(true);
            }
        }

        private void AddOnlineButton()
        {
            foreach (var button in GetComponentsInChildren<NavigatableButton>(true))
            {
                if (button.name != "Quickplay") continue;
                var online = Instantiate(button, button.transform.parent);
                online.name = "Online co-op";
                online.transform.SetSiblingIndex(button.transform.GetSiblingIndex() + 1);
                online.GetComponentInChildren<TextMeshProUGUI>(true).text = "Online co-op";
                online.ReplaceOnClickEvent(YARG.Online.OnlineMenu.Show);
                if (online.TryGetComponent<UnityEngine.UI.Button>(out var uiButton))
                    uiButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                var group = button.GetComponentInParent<NavigationGroup>();
                group.InsertNavigatable(1, online);
                break;
            }
        }

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                new NavigationScheme.Entry(MenuAction.Select, "Menu.Main.GoToCurrentlyPlaying", CurrentlyPlaying)
            }, true));
        }

        private void OnDisable()
        {
            Navigator.Instance?.PopScheme();
        }

        public void CurrentlyPlaying()
        {
            MusicLibraryMenu.RequestGoToCurrentlyPlaying(MusicPlayer.NowPlaying);
            QuickPlay();
        }

        public void QuickPlay()
        {
            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.MusicLibrary, false);

            MusicLibraryMenu.LibraryMode = MusicLibraryMode.QuickPlay;

            menu.gameObject.SetActive(true);
        }

        public void Practice()
        {
            if (YARG.Online.OnlineSession.Current?.IsConnected == true)
            {
                DialogManager.Instance.ShowMessage("Online co-op", "Use Quickplay to choose a song for your room, or leave the room to practice.");
                return;
            }
            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.MusicLibrary, false);

            MusicLibraryMenu.LibraryMode = MusicLibraryMode.Practice;

            menu.gameObject.SetActive(true);
        }

        public void Profiles()
        {
            MenuManager.Instance.PushMenu(MenuManager.Menu.ProfileList);
        }

        public void Replays()
        {
            MenuManager.Instance.PushMenu(MenuManager.Menu.History);
        }

        public void Credits()
        {
            MenuManager.Instance.PushMenu(MenuManager.Menu.Credits);
        }

        public void Settings()
        {
            SettingsMenu.Instance.gameObject.SetActive(true);
        }

        public void Exit()
        {
#if UNITY_EDITOR

            UnityEditor.EditorApplication.isPlaying = false;

#else
			Application.Quit();

#endif
        }

        public void OpenDiscord()
        {
            Application.OpenURL("https://discord.gg/sqpu4R552r");
        }

        public void OpenTwitter()
        {
            Application.OpenURL("https://twitter.com/YARGGame");
        }

        public void OpenGithub()
        {
            Application.OpenURL("https://github.com/YARC-Official/YARG");
        }
    }
}
