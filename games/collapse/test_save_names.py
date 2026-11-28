"""Run the shipping formatter against fixed-width panels without needing Steam."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

import save_names


class SaveNameTests(unittest.TestCase):
    def test_rejects_unknown_game_before_compiling(self):
        with self.assertRaises(ValueError):
            save_names.build(b"unknown build", Path("."))

    def test_bundled_builder_matches_pinned_source(self):
        package = save_names.HERE / "vendor/mono.cecil.0.11.6.nupkg"
        self.assertEqual(hashlib.sha256(package.read_bytes()).hexdigest(), save_names.CECIL)

    @unittest.skipUnless(os.environ.get("COLLAPSE_TEST_GAME"), "set COLLAPSE_TEST_GAME for a patched copy")
    def test_build_matches_installed_game(self):
        game = Path(os.environ["COLLAPSE_TEST_GAME"])
        managed = game / "PoliticalSimulator_Data/Managed"
        original = (game / ".collapse-zh-24949409/Assembly-CSharp.dll.original").read_bytes()
        generated = save_names.build(original, managed)
        self.assertEqual(hashlib.sha256(generated).hexdigest(), save_names.PATCHED)
        self.assertEqual(generated, (managed / "Assembly-CSharp.dll").read_bytes())

    @unittest.skipUnless(os.name == "nt", "Windows Framework compiler required")
    def test_game_formatter_on_fixed_panels(self):
        csc = Path(os.environ["SystemRoot"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
        with tempfile.TemporaryDirectory() as tmp:
            exe = Path(tmp) / "SaveNameTests.exe"
            subprocess.run([str(csc), "/nologo", "/codepage:65001", "/out:" + str(exe),
                            str(save_names.HERE / "SaveNameLayout.cs"),
                            str(save_names.HERE / "DiplomacyLayout.cs"),
                            str(save_names.HERE / "VotingPanel.cs"),
                            str(save_names.HERE.parents[1] / "tools/SaveNameLayoutTests.cs")],
                           timeout=30, check=True)
            subprocess.run([str(exe)], timeout=15, check=True)


if __name__ == "__main__":
    unittest.main()
