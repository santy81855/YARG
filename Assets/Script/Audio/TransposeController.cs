// Assets/Scripts/Audio/TransposeController.cs
using System;

public static class TransposeController {
    public static event Action<float>? Changed;
    static float _semitones = 0f; // -12..+12 for example

    public static float Semitones => _semitones;

    public static void Set(float semitones) {
        // keep conservative range at first; widen later if you like
        var clamped = Math.Clamp(semitones, -6f, 6f);
        if (Math.Abs(clamped - _semitones) < 0.001f) return;
        _semitones = clamped;
        Changed?.Invoke(_semitones);
    }
}
