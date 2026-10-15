using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Helpers
{
    /// <summary>A small summary of charted vocal pitches; does not retain or modify the chart.</summary>
    public sealed class VocalPitchPreview
    {
        private readonly float?[] _solo;
        private readonly float?[] _harmony;
        private static readonly string[] _names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public VocalPitchPreview(SongChart chart)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            _solo = chart.Vocals.Parts.Select(HighestPitch).ToArray();
            _harmony = chart.Harmony.Parts.Select(HighestPitch).ToArray();
        }

        public float? Highest(Instrument instrument, int harmonyPart)
        {
            if (instrument is not (Instrument.Vocals or Instrument.Harmony)) return null;
            var pitches = instrument == Instrument.Harmony ? _harmony : _solo;
            return harmonyPart >= 0 && harmonyPart < pitches.Length ? pitches[harmonyPart] : null;
        }

        public float? HighestAcrossParts() => _solo.Concat(_harmony).Max();

        public static float? HighestPitch(VocalsPart part)
        {
            float? highest = null;
            var pending = new Stack<VocalNote>(part.NotePhrases.Select(phrase => phrase.PhraseParentNote));
            while (pending.Count > 0)
            {
                var note = pending.Pop();
                if (note.IsPercussion || note.IsPercussionPhrase) continue;
                if (!note.IsPhrase && !note.IsNonPitched && !float.IsNaN(note.Pitch) && !float.IsInfinity(note.Pitch))
                    highest = highest.HasValue ? Math.Max(highest.Value, note.Pitch) : note.Pitch;
                // Slide endpoints are child notes; the highest pitch can be on either endpoint.
                foreach (var child in note.ChildNotes) pending.Push(child);
            }
            return highest;
        }

        public static string NoteName(float pitch)
        {
            int midi = (int) Math.Round(pitch, MidpointRounding.AwayFromZero);
            int pitchClass = (midi % 12 + 12) % 12;
            int octave = (int) Math.Floor(midi / 12.0) - 1;
            return _names[pitchClass] + octave;
        }
    }
}
