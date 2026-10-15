import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

import package


class PackageChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.build = self.root / "build"
        for name in ("YARG-Singing.x86_64", "UnityPlayer.so",
                     "YARG-Singing_Data/Managed/Assembly-CSharp.dll",
                     "YARG-Singing_Data/StreamingAssets/lang/en-US.json",
                     "YARG-Singing_Data/StreamingAssets/aa/settings.json"):
            p = self.build / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text("runtime content")

    def tearDown(self):
        self.temp.cleanup()

    def run_package(self):
        with contextlib.redirect_stdout(io.StringIO()):
            package.package(self.build, self.root / "out", "linux", "test", "test-tag")

    def test_preserves_runtime_and_omits_editor_cache(self):
        cache = self.build / "YARG-Singing_Data/StreamingAssets/sources~/repo/.vscode/settings.json"
        cache.parent.mkdir(parents=True)
        cache.write_text("editor only")
        self.run_package()
        stage = self.root / "out/staging/YARG-Singing-test-linux-x64"
        self.assertEqual((stage / "YARG-Singing_Data/StreamingAssets/aa/settings.json").read_text(), "runtime content")
        self.assertFalse((stage / "YARG-Singing_Data/StreamingAssets/sources~").exists())
        self.assertEqual((stage / "YARG-Singing_Data/Managed/Assembly-CSharp.dll").read_text(), "runtime content")
        self.assertTrue((stage / "Play-YARG.sh").stat().st_mode & 0o111)
        self.assertEqual(json.loads((stage / "BUILD-INFO.json").read_text())["platform"], "linux")

    def test_rejects_user_settings(self):
        (self.build / "settings.json").write_text("private")
        with self.assertRaisesRegex(RuntimeError, "user settings"):
            self.run_package()

    def test_rejects_user_song_cache(self):
        (self.build / "songcache.bin").write_bytes(b"private")
        with self.assertRaisesRegex(RuntimeError, "song cache"):
            self.run_package()

    def test_rejects_incomplete_runtime(self):
        (self.build / "UnityPlayer.so").unlink()
        with self.assertRaisesRegex(RuntimeError, "Incomplete build"):
            self.run_package()


if __name__ == "__main__":
    unittest.main()
