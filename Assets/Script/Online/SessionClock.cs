using System;

namespace YARG.Online
{
    /// <summary>Estimates host time without using wall clocks or changing the local input clock.</summary>
    public sealed class SessionClock
    {
        private double _bestRoundTrip = double.PositiveInfinity;
        private double _lastSample;

        public bool IsSynchronized { get; private set; }
        public double Offset { get; private set; }
        public double RoundTripSeconds => IsSynchronized ? _bestRoundTrip : 0;

        public bool Observe(double sent, double received, double hostReceived, double hostSent)
        {
            double roundTrip = received - sent - (hostSent - hostReceived);
            double offset = ((hostReceived - sent) + (hostSent - received)) / 2;
            if (!IsFinite(roundTrip) || !IsFinite(offset) || roundTrip < 0 || roundTrip > 10 ||
                hostSent < hostReceived || received < sent)
                return false;

            // Refresh the sample window so slow clock drift can be corrected between rounds.
            if (received - _lastSample > 30) _bestRoundTrip = double.PositiveInfinity;
            if (roundTrip > _bestRoundTrip) return false;

            _bestRoundTrip = roundTrip;
            _lastSample = received;
            Offset = offset;
            IsSynchronized = true;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}