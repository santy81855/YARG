#!/usr/bin/env python3
"""Package a complete Unity player, with provenance and licenses, without user data."""
import argparse
import hashlib
import json
import shutil
import subprocess
import tarfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def editor_only(path):
    return any(part in (".git", ".vscode") or part.endswith("~") for part in path.parts)


def ignore_editor_files(folder, names):
    return [name for name in names if editor_only(Path(name))]


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def sha256(file):
    with file.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def source_archive(output, version, tag):
    if git("status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Commit tracked source changes before creating the source archive")
    name = f"YARG-Singing-{version}-source"
    archive = output / (name + ".tar.gz")
    files = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).split(b"\0")
    core = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT / "YARG.Core").split(b"\0")
    paths = [Path(p.decode()) for p in files if p and p != b"YARG.Core"]
    paths += [Path("YARG.Core") / p.decode() for p in core if p]
    with tarfile.open(archive, "x:gz", compresslevel=6) as tar:
        for relative in sorted(paths):
            file = ROOT / relative
            if file.is_file():
                with file.open("rb") as stream:
                    if stream.read(128).startswith(b"version https://git-lfs.github.com/spec/v1"):
                        raise RuntimeError(f"Unhydrated LFS asset: {relative}")
            tar.add(file, arcname=str(Path(name) / relative), recursive=False)
    print(json.dumps({"archive": str(archive), "bytes": archive.stat().st_size,
                      "sha256": sha256(archive)}))


def package(build, output, platform, version, tag):
    executable = "YARG-Singing.exe" if platform == "windows" else "YARG-Singing.x86_64"
    for required in (executable, "YARG-Singing_Data/Managed/Assembly-CSharp.dll",
                     "YARG-Singing_Data/StreamingAssets/lang/en-US.json",
                     "UnityPlayer.dll" if platform == "windows" else "UnityPlayer.so"):
        if not (build / required).is_file():
            raise RuntimeError(f"Incomplete build: {required}")
    addressables_settings = build / "YARG-Singing_Data/StreamingAssets/aa/settings.json"
    if any(file != addressables_settings and not editor_only(file.relative_to(build))
           for file in build.rglob("settings.json")) or any(build.rglob("songcache.bin")):
        raise RuntimeError("Build folder contains user settings or a song cache")

    name = f"YARG-Singing-{version}-{platform}-x64"
    stage = output / "staging" / name
    if stage.exists():
        raise FileExistsError(stage)
    stage.mkdir(parents=True)
    for item in build.iterdir():
        if item.name.endswith(("_BackUpThisFolder_ButDontShipItWithYourGame", "_BurstDebugInformation_DoNotShip")):
            continue
        if item.is_dir():
            shutil.copytree(item, stage / item.name, ignore=ignore_editor_files)
        else:
            shutil.copy2(item, stage / item.name)
    shutil.copy2(ROOT / "Tools/Release/README.txt", stage / "README.txt")
    shutil.copy2(ROOT / "LICENSE", stage / "LICENSE")
    shutil.copy2(ROOT / "Tools/Release/COPYING.GPL3", stage / "COPYING.GPL3")
    (stage / "THIRD-PARTY-NOTICES.md").write_text(
        "# YARG credits and bundled components\n\n" +
        (ROOT / "README.md").read_text().split("## 🛡️ License", 1)[1].split("## 💸 Donate", 1)[0],
        encoding="utf-8")
    licenses = stage / "ThirdPartyLicenses"
    shutil.copytree(ROOT / "Tools/Release/Licenses", licenses / "Fonts")
    # Capture the licenses from the dependency versions actually installed for this build.
    for base in (ROOT / "Assets", ROOT / "Library/PackageCache", ROOT / "YARG.Core"):
        for file in base.rglob("*"):
            if not file.is_file() or file.suffix == ".meta":
                continue
            lower = file.name.lower()
            if any(word in lower for word in ("license", "licence", "notice", "ofl", "copying")):
                # Do not collect source files whose class name happens to contain License.
                if file.suffix.lower() not in ("", ".txt", ".md", ".html", ".rtf"):
                    continue
                dest = licenses / file.relative_to(ROOT)
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(file, dest)
    if platform == "linux":
        launcher = stage / "Play-YARG.sh"
        launcher.write_text('#!/usr/bin/env bash\nset -e\ncd -- "$(dirname -- "${BASH_SOURCE[0]}")"\nexec ./YARG-Singing.x86_64 "$@"\n')
        launcher.chmod(0o755)
        (stage / executable).chmod(0o755)
    info = {"product": "YARG Singing (unofficial)", "version": version, "platform": platform,
            "architecture": "x86_64", "editor": "6000.3.5f2", "backend": "Mono",
            "source_commit": git("rev-parse", "HEAD"), "source_tag": tag,
            "source_url": f"https://github.com/santy81855/YARG/tree/{tag}",
            "core_commit": subprocess.check_output(["git", "rev-parse", "HEAD"],
                                                  cwd=ROOT / "YARG.Core", text=True).strip()}
    (stage / "BUILD-INFO.json").write_text(json.dumps(info, indent=2) + "\n")
    if platform == "windows":
        archive = output / (name + ".zip")
        with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
            for file in sorted(stage.rglob("*")):
                if file.is_file():
                    z.write(file, file.relative_to(stage.parent))
    else:
        archive = output / (name + ".tar.gz")
        with tarfile.open(archive, "x:gz", compresslevel=6) as tar:
            tar.add(stage, arcname=stage.name)
    print(json.dumps({"archive": str(archive), "bytes": archive.stat().st_size,
                      "sha256": sha256(archive)}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--platform", choices=("linux", "windows", "source"), required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--tag", required=True)
    a = parser.parse_args()
    a.output.mkdir(parents=True, exist_ok=True)
    if a.platform == "source":
        source_archive(a.output.resolve(), a.version, a.tag)
    else:
        if a.build is None:
            parser.error("--build is required for player packages")
        package(a.build.resolve(), a.output.resolve(), a.platform, a.version, a.tag)
