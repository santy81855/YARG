#!/usr/bin/env python3
"""Allow HIDrogen's managed GameInput bindings when cross-building Windows on Linux.

HIDrogen 0.5.3 compiles its Windows backend for UNITY_STANDALONE_WIN, including in
the Linux editor targeting Windows, but its explicitly referenced managed DLL is
marked for Windows editors only. Its initialization already checks the host OS.
Change only the cached managed DLL's editor import OS; player import flags stay
unchanged. Re-run after a fresh package restore if necessary.
"""
import argparse
from pathlib import Path


def prepare(project):
    matches = list((project / "Library/PackageCache").glob(
        "com.thenathannator.hidrogen@*/HIDrogen/Plugins/SharpGameInput.v0.dll.meta"))
    if len(matches) != 1:
        raise RuntimeError("Import the project once, then retry; expected one HIDrogen managed bindings DLL")
    meta = matches[0]
    text = meta.read_text()
    old = "        OS: Windows\n"
    new = "        OS: AnyOS\n"
    if new in text:
        print("Managed GameInput bindings already enabled for cross-platform editors")
        return
    if text.count(old) != 1:
        raise RuntimeError("Unexpected plugin metadata; inspect it before updating")
    meta.write_text(text.replace(old, new))
    print("Enabled managed GameInput bindings in the build project's editor:", meta)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    prepare(parser.parse_args().project.resolve())
