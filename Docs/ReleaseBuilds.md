# YARG Singing releases

YARG Singing is an unofficial, non-commercial fork of YARG. A release consists of
matching Linux x86_64 and Windows x86_64 Mono players, the corresponding source
archive with hydrated Git LFS assets and the pinned YARG.Core submodule, checksums,
and release notes. Songs, user settings, microphone profiles and conversion work
are not part of a release.

## Build requirements

- Unity **6000.3.5f2** with Linux Standalone support and Windows Build Support
  (Mono). Use the exact project editor version.
- A checkout with Git LFS objects downloaded and submodules initialized:
  `git submodule update --init --recursive` and `git lfs pull`.
- Restore the project's NuGet dependencies (NuGetForUnity), and build/link the
  YARG.Core DLL as described in the upstream project setup instructions.
- Python 3.11 or newer for packaging.

The exact Windows module for this Linux editor is listed in Unity's official
[6000.3.5f2 release](https://unity.com/releases/editor/whats-new/6000.3.5f2).
Unity's release API advertises a PKG for this Linux module. Its gzip/cpio payload
can be extracted from the XAR package into
`Editor/Data/PlaybackEngines/WindowsStandaloneSupport`; verify the published
integrity digest before installing. No Windows IL2CPP toolchain is required for
the Mono player.

## Build commands

Use a clean checkout or disposable project snapshot while another editor has the
main project open. Set the executable path for your Unity installation. Start
Unity with the appropriate build target so platform conditionals and native
plugin selection are correct.

```sh
Unity -batchmode -nographics -quit -projectPath /path/to/project \
  -buildTarget Linux64 -executeMethod Editor.Build.BuildSingingRelease.Build \
  -yarg-platform linux -yarg-version 0.1.0-beta.1 \
  -yarg-output /path/to/players/linux/YARG-Singing.x86_64 -logFile linux-build.log

Unity -batchmode -nographics -quit -projectPath /path/to/project \
  -buildTarget Win64 -executeMethod Editor.Build.BuildSingingRelease.Build \
  -yarg-platform windows -yarg-version 0.1.0-beta.1 \
  -yarg-output /path/to/players/windows/YARG-Singing.exe -logFile windows-build.log
```

Require a successful build result and `YARG_SINGING_RELEASE_SUCCESS` in each log.
The builder temporarily uses product name `YARG Singing`, includes the version
resource, and forces Mono. Its settings use
`YARC/YARG Singing/dev/` under Unity's per-user persistent data directory. This
keeps regular YARG and the existing local development build separate. The builder
restores the project product name, version, and scripting backend afterward.

## Checks and packaging

Run the checks in `Tests/SongPresets`, `Tests/SongKey` and `Tests/Online` before
tagging. Song-key audio checks need the platform's BASS libraries on the native
library search path. Review the changes and commit the source before building;
the package manifest records the current source commit and pinned core commit.

```sh
python3 Tools/Release/package.py --build /path/to/players/linux \
  --output Builds/Releases --platform linux --version 0.1.0-beta.1 \
  --tag singing-v0.1.0-beta.1
python3 Tools/Release/package.py --build /path/to/players/windows \
  --output Builds/Releases --platform windows --version 0.1.0-beta.1 \
  --tag singing-v0.1.0-beta.1
python3 Tools/Release/package.py --output Builds/Releases --platform source \
  --version 0.1.0-beta.1 --tag singing-v0.1.0-beta.1
```

The packager copies complete runtime folders, omits Unity's explicitly marked
debug/backup folders, preserves Linux executable permissions, includes a launcher,
and adds build provenance, instructions, source licenses and dependency notices.
It rejects builds containing a settings file or song cache. Keep source archives
and logs outside the player folders.

Also produce a source archive from the committed tracked files, replacing LFS
pointers with actual local asset content and including the tracked files from
the pinned submodule checkout. Exclude Library, Temp, Builds, local NuGet caches,
and untracked song-conversion work. Keep it below GitHub's individual asset limit.
Generate SHA-256 checksums for all three archives, then reopen/test each archive.

Smoke-test Linux with an isolated persistent-data directory. A Windows startup
check under Wine is useful but does not verify real Windows microphone,
controller, rendering, or audio behavior. Record exactly what was tested in the
release notes, and mark early builds as pre-releases.

## GitHub

Release repository: <https://github.com/santy81855/YARG>. Push a dedicated singing
branch and version tag; do not push releases to YARC-Official/YARG. Upload the two
player packages, complete source archive and checksums to one versioned release.
The release notes should link to the exact source commit, explain the settings
folder, identify known limitations, and describe the tests actually performed.
Online co-op requires Unity Services/Relay configuration and native cross-platform
internet testing; this release process does not provision that service.
