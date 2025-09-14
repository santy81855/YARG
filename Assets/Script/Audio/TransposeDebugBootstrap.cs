using UnityEngine;

public static class TransposeDebugBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init() {
        var go = new GameObject("TransposeDebugControls");
        go.AddComponent<TransposeDebugControls>();
    }
}
