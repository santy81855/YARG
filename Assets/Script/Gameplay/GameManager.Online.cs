using System.Linq;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        public bool IsOnline { get; private set; }

        public void StopOnlineSong()
        {
            _songRunner.Pause();
            enabled = false;
        }

        public void WaitForOnlineStart()
        {
            _songRunner.Pause();
        }

        public void StartOnlineSong(double elapsed)
        {
            // Preserve chart offset and each computer's own audio/video calibration.
            SetSongTime(_songRunner.SongOffset - SONG_START_DELAY * SongSpeed + elapsed, 0);
            _songRunner.Resume();
            _songRunner.Update();
        }

        private int FinalOnlineStars()
        {
            var result = new YARG.Core.Engine.EngineManager
            {
                Score = _players.Sum(player => player.Score + player.BandBonusScore),
                StarScoreThresholds = EngineManager.StarScoreThresholds
            };
            result.UpdateStars();
            return (int) result.Stars;
        }

        public void ApplyOnlinePause(bool paused)
        {
            if (paused)
            {
                _songRunner.Pause();
                PauseCore(true);
            }
            else
            {
                // Resume at the scheduled time without locally rewinding a shared performance.
                _songRunner.Resume();
                ResumeCore();
            }
        }
    }
}
