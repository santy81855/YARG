YARG Singing — unofficial singing-focused YARG build

This is a modified version of Yet Another Rhythm Game, based on YARC-Official/YARG.
It is not an official YARC release. Distributed free for non-commercial use.

Getting started
---------------
Extract the entire archive to a writable folder. Keep the executable, its _Data
folder, and the other included runtime files together. Unity is not required.

Windows: install Microsoft's Visual C++ v14 Redistributable (x64) if needed, then
open YARG-Singing.exe. Official installer: https://aka.ms/vc14/vc_redist.x64.exe
Linux: run ./Play-YARG.sh. If needed, mark it executable with chmod +x Play-YARG.sh.

Connect your microphone/controller, create or connect your player profile, then
add your own song folder in Settings > Song Manager. No songs are included.

Before each song, Shared song setup lets everyone on this computer agree on the
key, original-vocals On/Off, and playback speed. Continue opens each player's own
instrument/difficulty/harmony setup. Back from the first player returns to shared
setup. Choices save automatically per chart, shared by all local profiles.

Vocals Off mutes a separate vocal stem; it cannot remove vocals from a combined
backing track. Key changes range from -12 to +12 semitones and change playback,
vocal targets, and scoring together. The highest-note preview covers all parts.

Settings
--------
This fork uses the product name YARG Singing and its own settings folder:
  Linux:   ~/.config/unity3d/YARC/YARG Singing/dev/
  Windows: %USERPROFILE%\AppData\LocalLow\YARC\YARG Singing\dev\
Regular YARG and the existing local development build keep their own settings.
Advanced users can choose a different folder with -persistent-data-path PATH.

Testing and limitations
-----------------------
This is an early beta. See the GitHub release notes for the checks actually run
on each platform and remaining limitations. Windows microphone/controller play
needs testing on a real Windows machine; Wine startup checks do not cover that.
The online co-op prototype requires Unity Services/Relay project configuration
and cross-platform internet testing. This package does not configure that service.

Source and licenses
-------------------
Fork:     https://github.com/santy81855/YARG
Upstream: https://github.com/YARC-Official/YARG
The matching source tag/commit is recorded in BUILD-INFO.json and release notes.
Clone the tagged source with submodules and Git LFS to rebuild. Build instructions
are in Docs/ReleaseBuilds.md. LICENSE, COPYING.GPL3, THIRD-PARTY-NOTICES.md and
ThirdPartyLicenses/ preserve license and attribution information. YARG source
is LGPL-3.0-or-later; bundled components/assets retain their respective licenses.
The accompanying source archive contains the fork's source, assets and pinned
YARG.Core checkout; package dependencies are listed in their manifests.

Do not move personal songs, settings, profiles, or conversion backups into these
download archives when sharing them.
