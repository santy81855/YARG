using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Input;

namespace YARG.Online
{
    /// <summary>Ordered chart-relative inputs and the time the sender has completed.</summary>
    public sealed class RemoteInputStream
    {
        private readonly List<GameInput> _inputs;
        private int _next;
        public double ThroughTime { get; private set; } = double.MinValue;

        public RemoteInputStream(List<GameInput> inputs) => _inputs = inputs;

        public void Append(long firstInput, double through, GameInput[] inputs, double songLength)
        {
            if (firstInput != _inputs.Count || inputs == null || inputs.Length > 256 ||
                _inputs.Count + inputs.Length > 2000000 || !Finite(through) ||
                through < ThroughTime || through < -3600 || through > songLength + 120)
                throw new InvalidDataException("Invalid remote performance sequence.");
            double previous = _inputs.Count == 0 ? -3600 : _inputs[^1].Time;
            foreach (var input in inputs)
            {
                if (!Finite(input.Time) || input.Time < previous || input.Time > songLength + 120 ||
                    input.Action < 0 || input.Action > 256 || float.IsNaN(input.Axis) || float.IsInfinity(input.Axis))
                    throw new InvalidDataException("Invalid remote input.");
                previous = input.Time;
            }
            _inputs.AddRange(inputs);
            ThroughTime = through;
        }

        public bool TryDequeue(out GameInput input)
        {
            if (_next < _inputs.Count && _inputs[_next].Time <= ThroughTime)
            {
                input = _inputs[_next++];
                return true;
            }
            input = default;
            return false;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
