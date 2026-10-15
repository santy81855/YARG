using System.Threading.Tasks;
using UnityEngine;
using YARG.Menu;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Online
{
    public static class OnlineMenu
    {
        public static void Show()
        {
            var session = OnlineSession.Instance;
            var dialogs = DialogManager.Instance;
            dialogs.ClearDialog();
            var list = dialogs.ShowList(session.IsConnected ? "Online co-op · " + session.JoinCode : "Online co-op");
            if (!session.IsConnected)
            {
                list.AddListButton("Create room", () => { dialogs.ClearDialog(); _ = Connect(true); }, false);
                list.AddListButton("Join with code", () =>
                {
                    dialogs.ClearDialog();
                    dialogs.ShowRenameDialog("Enter your friend's room code", code => { _ = Connect(false, code); });
                }, false);
            }
            else
            {
                list.AddListButton("Copy room code", () => GUIUtility.systemCopyBuffer = session.JoinCode, false);
                list.AddListButton("Room status", () =>
                {
                    dialogs.ClearDialog();
                    session.ShowRoomStatus();
                }, false);
                if (session.IsHost)
                {
                    list.AddListButton("Choose song", () =>
                    {
                        dialogs.ClearDialog();
                        if (!session.PeerConnected || PlayerContainer.Players.Count == 0)
                        {
                            dialogs.ShowMessage("Online co-op", !session.PeerConnected ?
                                "Wait for your friend to join, then choose a song." : "Activate your player profile first.");
                            return;
                        }
                        var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.MusicLibrary, false);
                        MusicLibraryMenu.LibraryMode = MusicLibraryMode.QuickPlay;
                        menu.gameObject.SetActive(true);
                    }, false);
                }
                if (session.HasSongSelection)
                    list.AddListButton("Cancel song selection", () => session.ReturnToRoom());
                list.AddListButton("Leave room", () => { session.Leave(); });
            }
            list.AddListButton("How it works", () =>
            {
                dialogs.ClearDialog();
                dialogs.ShowMessage("Online co-op", "Activate your own player profile and microphone or controller. " +
                    "Create a room and share its code, or join your friend's room.\n\n" +
                    "The host chooses a song. If you don't have it, it downloads before play. " +
                    "Each computer chooses its own difficulty and song key. Your input is processed locally.\n\n" +
                    "Either player can pause or restart. Online sessions use no fail, and show results without saving scores or replays.");
            }, false);
        }

        private static async Task Connect(bool host, string code = null)
        {
            // RenameDialog clears itself after invoking its callback.
            await Task.Yield();
            var dialogs = DialogManager.Instance;
            dialogs.ClearDialog();
            if (PlayerContainer.Players.Count == 0)
            {
                dialogs.ShowMessage("Online co-op", "Activate your player profile before creating or joining a room.");
                return;
            }
            dialogs.ShowMessage("Online co-op", host ? "Creating room…" : "Joining room…");
            await OnlineSession.Instance.Connect(host, code);
            if (OnlineSession.Instance.IsConnected)
            {
                dialogs.ClearDialog();
                Show();
            }
        }
    }
}
