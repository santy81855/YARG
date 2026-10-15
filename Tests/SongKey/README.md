Song key changes are available directly below Resume in the quick play, practice,
and setlist pause menus. Select **Song key (semitones)** and press Confirm, then
use Up/Down for single-semitone adjustments. Confirm or Back finishes editing;
the yellow action restores the original key. With the mouse, click the − and +
buttons beside the current value, or scroll over the setting. Each click changes
the key by one semitone. Left/Right also adjust the value while editing. The
range is −12 to +12; zero means the original key. Buttons disable at their limits.

The full song mix changes key without changing its tempo. Vocal tubes, slides,
pitch guides, needles, and scoring follow the selected key, including harmony
parts. The choice is saved per chart hash in `SongKeyOffsets` in YARG's existing
`settings.json`, so it survives song restarts and game restarts. Chart and audio
files are never rewritten.

The scoring engine receives microphone pitches converted back into the chart's
original key. Saved replay inputs therefore remain compatible with the existing
replay format. Replays play in the original key; key controls and remembered keys
are disabled while watching or playing alongside a replay.

Run the headless regression checks on Linux with the .NET 8 SDK:

```sh
LD_LIBRARY_PATH="$PWD/Assets/Plugins/BassNative/Linux/x86_64${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" \
  dotnet run --project Tests/SongKey/SongKey.Checks.csproj
```

The checks compile the production conversion and GameManager key code, exercise
the real YARG.Core vocals engine for all 25 keys and all three harmony parts,
and decode generated tones through the shipped BASS/Fx libraries to measure
pitch and duration. Unity-owned services are replaced by test doubles for the
GameManager checks. These checks do not compile the full Unity game or render
the menus.

For the remaining visual and device checks, open the project with Unity
6000.3.5f2 following the root README. In Play mode or a Linux build:

1. Start a song with a microphone. Pause and verify Resume is followed by the key
   control, with matching keyboard/controller navigation order.
2. Try −3, +3, and 0. Resume each time: verify the music changes key immediately,
   the vocal guides/needle agree with the new key, and lyrics stay synchronized.
3. Test a pitch slide and each harmony part. Talkies and percussion should work
   normally. Test both mouse buttons, repeated clicks, the disabled buttons at
   −12/+12, keyboard/controller adjustment, and the original-key action.
4. Restart the song, return to it from the library, and relaunch the game; its key
   should persist. Play another song and verify its independent key.
5. In practice, change the key, change speed, loop a section, and reset practice.
   Check both normal and chipmunk speed modes. In a setlist, verify each next song
   restores its own key.
6. Save and watch a replay of a transposed performance. Verify it plays and scores
   correctly in the original key and offers no key control.

The pre-song vocal range checks cover named notes/octaves, transposition, slide
endpoints, percussion/talkie exclusion, and independent harmony parts.
