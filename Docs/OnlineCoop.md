# Online co-op

Two computers can play one song together using Unity Relay and a room code.
Each computer handles its own microphone/controller and scoring immediately.
Only timestamped inputs and results cross the connection; microphone audio is
not transmitted. Your friend's performance appears after the network delay.

The host chooses the song and playback speed. Each computer chooses its own
key before starting, directly below Ready in the difficulty menu, using minus/plus
controls. The highest charted note for the selected vocal part updates with each
change (for example, A4 becomes G#4 after lowering by one semitone). The same
controls remain available in the pause menu. The audio and
notes on that computer change together, and the key is remembered per song.
Singing inputs are converted back to chart pitch before sending, so one singer
can sing lower and the other higher without changing each other's scoring.

The host selects instruments first, then the guest selects their instruments
and difficulties. Vocalists share solo/harmony mode and vocal chart modifiers,
as they do in local multiplayer; individual difficulties and harmony parts are
independent. The initial limit is two computers, four players on each computer,
and seven players total.

## One-time Unity service setup

The source currently has no Unity Cloud project ID. Internet play requires
linking the project before producing either platform's build:

1. Sign in at <https://cloud.unity.com>. Under Home > Projects, create a project
   for your YARG fork.
2. In Unity, open Edit > Project Settings > Services. Choose Use an existing
   Unity project ID, select that organization/project, and click Link project ID.
3. In that project's Unity Dashboard, open Multiplayer > Relay and finish its
   Get started/setup flow if offered. The game uses anonymous authentication;
   players do not need to sign in themselves.
4. Produce matching Linux and Windows builds from this linked project. Both
   must contain the same Unity Cloud project ID and this multiplayer code.
   The editor installed on this machine currently has Linux build support only;
   a Windows build also needs Windows Build Support (Mono), or a Windows editor.

The required SDK packages and network components are configured by the code.
You do not need to add a NetworkManager to any scene, configure a router, run a
server, or give your friend Unity account credentials.

Unity references:
[linking an existing project](https://docs.unity.com/en-us/services/getting-started#link-your-project-in-the-unity-editor),
[Relay and Netcode integration](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo).

## Playing

1. Both players activate their usual player profiles and input devices.
2. Open **Online co-op**, directly below **Quickplay** on the main menu.
3. One player chooses **Create room** and shares the code. The other chooses
   **Join with code**. Copy room code and room status are available in this menu.
4. The host chooses a song through **Choose song** or ordinary Quickplay.
5. If the guest lacks that chart, the game transfers the song first, showing
   progress. Song folders and SNG/YARGSONG packages are supported. For CON/ExCON
   containers, both players currently need their own matching song.
6. The host chooses instruments/difficulties, followed by the guest. Both games
   load, wait for each other, and use a common clock to schedule the start.
   Vocalists can choose their key below Ready before committing; the highest-note
   context comes from pitched notes in their selected part, including slides.
7. Either player can pause the song for both computers. Either can resume; a
   shared countdown resumes playback without the ordinary local pause rewind.
   While paused, each computer can adjust its own song key.
8. Both results screens show every player's scores. Final results come from the
   computer that recorded each performance. Continue returns both to the room;
   restart reloads the song on both computers.

Downloads live under `online-songs` in YARG's existing settings/data directory,
separate from the normal song library. Verified downloads can be reused in a
later room. They can be removed by deleting this cache while the game is closed.
Files are size limited, checked with SHA-256, and checked for path traversal and
filenames that would conflict on Windows before extraction.

This first version uses no fail online. It displays results but does not write
online high scores or replay files into the ordinary local score history.
Practice and setlists are not part of online co-op yet. A disconnect ends the
room; there is no host migration or mid-song rejoining.

## Validation

`Tests/Online` covers the actual vocal engine with independent keys and delayed
input batches, host-clock estimation, final score serialization, and song
packages including invalid archives. Existing `Tests/SongKey` also checks actual
Linux BASS audio pitch/duration and per-song settings.

A full Unity project import/script compile and a Linux test-player build have
passed. Two processes using the game's real networking code passed connection
approval, handshake, clock synchronization, and twenty large ordered messages
in both directions. These checks use loopback transport.

A live Windows/Linux Relay session, complete song-sharing/gameplay UI flow, and
real microphone/controller play across two homes still need acceptance testing.
The live Relay check requires the account setup above. Compilation and loopback
checks do not establish that internet play is working for a particular account.
