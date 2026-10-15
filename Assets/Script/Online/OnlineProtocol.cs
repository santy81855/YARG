using System;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Input;

namespace YARG.Online
{
    // Explicit DTOs: never enable JSON TypeNameHandling for peer data.
    [Serializable]
    internal sealed class OnlineMessage
    {
        public string Type;
        public string Round;
        public string Text;
        public string Hash;
        public string Data;
        public string Digest;
        public long Size;
        public int Index;
        public float Speed;
        public double Sent;
        public double Received;
        public double At;
        public double SongTime;
        public bool Flag;
        public OnlineProfile[] Profiles;
        public OnlineInput[] Inputs;
        public OnlineFinalScore[] Results;
    }

    [Serializable]
    internal sealed class OnlineProfile
    {
        public YargProfile Profile;
        public EnginePreset Engine;
        public RockMeterPreset RockMeter;
        public bool VoiceStarPower;
    }

    [Serializable]
    internal sealed class OnlineFinalScore
    {
        public string Stats;
        public float AverageMultiplier;
    }

    [Serializable]
    internal struct OnlineInput
    {
        public double Time;
        public int Action;
        public int Value;

        public OnlineInput(GameInput input)
        {
            Time = input.Time;
            Action = input.Action;
            Value = input.Integer;
        }

        public GameInput ToInput() => new(Time, Action, Value);
    }
}
