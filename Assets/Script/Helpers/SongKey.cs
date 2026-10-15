using System;
using YARG.Core.Input;

namespace YARG.Helpers
{
    /// <summary>Converts between the chart's original key and the selected playback key.</summary>
    public static class SongKey
    {
        public const int MIN_SEMITONES = -12;
        public const int MAX_SEMITONES = 12;

        public static int Clamp(int semitones) => Math.Clamp(semitones, MIN_SEMITONES, MAX_SEMITONES);

        public static float ToChartPitch(float pitch, int semitones) => pitch - semitones;

        public static float ToPlaybackPitch(float pitch, int semitones) => pitch + semitones;

        public static GameInput ToChartInput(GameInput input, int semitones) =>
            input.GetAction<VocalsAction>() == VocalsAction.Pitch
                ? new GameInput(input.Time, input.Action, ToChartPitch(input.Axis, semitones))
                : input;

        public static double AudioPitch(int semitones, float speed, bool chipmunk) =>
            Math.Clamp(semitones + (chipmunk ? 12 * Math.Log(speed, 2) : 0), -60, 60);
    }
}