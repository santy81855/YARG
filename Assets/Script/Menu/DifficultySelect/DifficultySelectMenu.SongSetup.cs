using YARG.Localization;
using YARG.Player;

namespace YARG.Menu.DifficultySelect
{
    public partial class DifficultySelectMenu
    {
        private void ShowSongSetup()
        {
            _menuState = State.SongSetup;
            UpdateForPlayer();
        }

        private void CreateSongSetupMenu()
        {
            _text.text = LocalizeHeader("SongSetup");
            _loadingPhrase.text = LocalizeHeader("SongSetupDescription");
            ShowWarning(null);

            CreateItem(Localize.Key("Menu.Common.Continue"), true, _difficultyGreenPrefab, ContinueFromSongSetup);
            CreateSongKeyItem();
            CreateVocalsVolumeItem();
        }

        private void ContinueFromSongSetup()
        {
            CommitSongSpeed(_speedInput.text);
            _lastMenuState = State.Main;
            ChangePlayer(0);
        }

        private void NavigateBack()
        {
            if (_playerIndex >= PlayerContainer.Players.Count &&
                YARG.Online.OnlineSession.Current is { IsConnected: true } waitingRoom)
            {
                waitingRoom.ReturnToRoom();
                return;
            }

            if (_menuState == State.SongSetup)
            {
                if (YARG.Online.OnlineSession.Current is { IsConnected: true } online)
                    online.ReturnToRoom();
                else
                    MenuManager.Instance.PopMenu();
            }
            else if (_menuState == State.Main)
            {
                if (_playerIndex == 0) ShowSongSetup();
                else ChangePlayer(-1);
            }
            else
            {
                _menuState = State.Main;
                UpdateForPlayer();
            }
        }
    }
}
