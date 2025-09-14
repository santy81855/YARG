using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // new input system
#endif

public class TransposeDebugControls : MonoBehaviour
{
    void Awake() => DontDestroyOnLoad(gameObject);

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        // Use F9/F10 to avoid layout/shift quirks
        if (kb.f9Key.wasPressedThisFrame)
            TransposeController.Set(TransposeController.Semitones - 1);

        if (kb.f10Key.wasPressedThisFrame)
            TransposeController.Set(TransposeController.Semitones + 1);

        // (Optional) also support - / = and keypad +/- if you want:
        if ((kb.minusKey?.wasPressedThisFrame ?? false) ||
            (kb.numpadMinusKey?.wasPressedThisFrame ?? false))
            TransposeController.Set(TransposeController.Semitones - 1);

        if ((kb.equalsKey?.wasPressedThisFrame ?? false) ||
            (kb.numpadPlusKey?.wasPressedThisFrame ?? false))
            TransposeController.Set(TransposeController.Semitones + 1);
#else
        // Old Input Manager fallback
        if (UnityEngine.Input.GetKeyDown(KeyCode.F9) ||
            UnityEngine.Input.GetKeyDown(KeyCode.Minus) ||
            UnityEngine.Input.GetKeyDown(KeyCode.KeypadMinus))
            TransposeController.Set(TransposeController.Semitones - 1);

        if (UnityEngine.Input.GetKeyDown(KeyCode.F10) ||
            UnityEngine.Input.GetKeyDown(KeyCode.Equals) ||
            UnityEngine.Input.GetKeyDown(KeyCode.KeypadPlus))
            TransposeController.Set(TransposeController.Semitones + 1);
#endif
    }
}
