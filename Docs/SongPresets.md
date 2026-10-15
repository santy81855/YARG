# Per-song singing presets

One shared song-setup screen appears before individual player setup. It remembers
each chart's key, original-vocals toggle, and song speed automatically. There is
no separate Save button. Key uses the existing minus/plus controls and highest-note
preview; vocals uses the checkbox next to those controls; speed uses the existing
percentage field. The highest-note preview covers all vocal parts, including
harmonies, because players choose their individual parts afterward.

Choose Continue to configure each player's instrument, difficulty, harmony part,
and modifiers. Key, vocals, and speed controls appear only on shared setup.
Back from the first player's main menu returns to shared setup for intentional
changes. Back from later players returns to the preceding player; Back from
shared setup returns to the song library (or the room when playing online).
Returning to shared setup retains the current choices.

- A song without saved choices starts at its original key, vocals On, and 100%
  speed. Vocals On means 100% of the vocal stem's volume, and Off means 0%.
- Returning to that song restores the saved choices, including after restarting
  the game. Switching songs does not carry the previous song's speed or vocals
  choice into the next song.
- Choices belong to the chart hash, so moving a song folder or rescanning the
  library preserves them. Copies with the same chart hash share a preset.
- Presets belong to this installation's settings, not a microphone or player
  profile. All local profiles use the same saved choices for a given song.
- The key and vocals choice apply to the audio on this computer. Local players
  share that audio. Different computers in online play keep independent key and
  vocals choices; the host chooses the shared speed. A guest's offline speed
  preference is preserved when playing at the host's speed.
- Shows retain one speed for the whole show and do not overwrite the first
  song's saved speed. Each song still loads its own saved vocals choice.
- Replays retain their existing speed/key behavior and do not overwrite presets.

The vocals choice controls a separate vocal stem when one exists. Turning it Off
cannot remove a singer embedded in a combined backing track.

## Storage and audio behavior

Presets are stored in YARG's existing `settings.json`. Existing `SongKeyOffsets`
entries are retained for compatibility; `SongPlaybackPresets` stores
`VocalsEnabled` and integer `SpeedPercent`, indexed by the same chart hash. An
older settings file needs no migration. Speed is limited to the existing
10–5000% range. Invalid speed text falls back to 100%.

The speed field saves when editing ends and is committed again when Ready is
pressed, so clicking Ready directly after typing uses the current value. The
vocals checkbox saves immediately. Save failures display a toast while keeping
the current playback choice active for this session.

The vocals preset temporarily overrides the vocal stem's playback volume; it
does not overwrite the global vocal-volume preference. The global volume is
restored when leaving the pre-song menu or gameplay, and gameplay reapplies the
selected song's preset during loading. Mute-on-miss restores the effective
volume, so it cannot accidentally enable a muted vocal stem. Adjusting the
global vocal-volume slider during a song still works and overrides the preset
for that session; the saved per-song toggle remains unchanged.

## Validation

Run the lightweight checks with .NET 8:

```sh
dotnet run --project Tests/SongPresets/SongPresets.Checks.csproj
```

These compile the production preset data, storage/audio helpers, and pre-song
menu handlers unchanged, replacing Unity services with small stand-ins. They
check JSON round trips and legacy key compatibility, isolation between songs,
exact vocals volumes, global-volume restoration, field commits, online speed
authority, show/replay protection, invalid inputs, and save-error feedback.
They also check shared setup, multiple-player navigation, and protection against
late callbacks changing shared choices after continuing to individual setup.
They never open or write a user's settings file.

For a manual check in the game:

1. Select song A; choose a lower key, vocals Off, and 85% speed. Start it and
   confirm that the vocal stem is silent, the key is lower, and playback is slower.
2. Select an unconfigured song B; confirm original key, vocals On, and 100% speed.
3. Reopen song A, then quit and restart the game and reopen it again. Confirm
   all three choices are restored.
4. Change A to vocals On and 100% speed and confirm those values persist too.
5. Connect two local profiles. Confirm shared setup appears first and both
   individual setup menus contain only player-specific choices. Back from player
   two should return to player one; Back again should return to shared setup with
   the same key, vocals, and speed choices.

The code is shared between Linux and Windows. Platform builds and testing are
still required before publishing release archives.

Local verification on 2026-10-15: all 53 preset checks and the existing song-key
checks passed, Unity 6000.3.5f2 produced the Linux standalone build successfully,
and that player completed startup, loaded the existing library, and initialized
BASS audio in a headless check. The Start-menu shortcut uses this updated build.
The interactive checks above and a Windows build remain part of release testing.
