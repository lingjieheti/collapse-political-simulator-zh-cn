"""Run the shipping save transaction against real Windows files, with synthetic game state."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


HERE = Path(__file__).resolve().parent


@unittest.skipUnless(os.name == "nt", "Windows file sharing and .NET Framework required")
class SaveTransactionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.build = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.build.cleanup)
        cls.driver = Path(cls.build.name) / "SaveTransactions.exe"
        compiler = Path(os.environ["SystemRoot"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
        subprocess.run([str(compiler), "/nologo", "/codepage:65001", "/out:" + str(cls.driver),
                        str(HERE / "TransactionalSaves.cs"),
                        str(HERE.parents[1] / "tools/TransactionalSavesTests.cs")],
                       check=True, timeout=30)

    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.slot = self.root / "saves" / "Campaign"
        self.stage = self.root / "saves/.collapse-save-transaction"

    def invoke(self, *args):
        subprocess.run([str(self.driver), *args], cwd=self.root, check=True, timeout=15)

    def pair(self):
        return tuple(self.slot.with_suffix(ext).read_bytes() for ext in (".save", ".smeta"))

    def test_locked_overwrite_preserves_both_files_and_retries(self):
        for ext in (".save", ".smeta"):
            with self.subTest(locked=ext):
                self.invoke("seed")
                original = self.pair()
                self.invoke("locked-overwrite", ext)
                self.assertEqual(self.pair(), original)
                self.invoke("retry")
                self.assertTrue(all(a != b for a, b in zip(original, self.pair())))
                self.invoke("read-new")

    def test_interrupted_overwrite_recovers_on_next_process_start(self):
        self.invoke("seed")
        original = self.pair()
        self.invoke("interrupt")
        self.assertFalse(self.slot.with_suffix(".smeta").exists())
        self.assertNotEqual(self.slot.with_suffix(".save").read_bytes(), original[0])
        self.invoke("scan")
        self.assertEqual(self.pair(), original)
        self.assertFalse((self.stage / "pending").exists())
        self.invoke("scan")
        self.assertEqual(self.pair(), original)

    def test_interrupted_first_save_leaves_no_phantom_slot(self):
        self.invoke("interrupt")
        self.assertTrue(self.slot.with_suffix(".save").exists())
        self.invoke("scan-empty")
        self.assertFalse(any(self.slot.parent.glob("Campaign.*")))
        self.assertFalse((self.stage / "pending").exists())

    def test_create_from_another_cache_preserves_existing_slot(self):
        self.invoke("seed")
        original = self.pair()
        self.invoke("create-again", "Campaign")
        self.assertEqual(self.pair(), original)
        if (self.slot.parent / "campaign.save").exists():
            self.invoke("create-again", "campaign")
            self.assertEqual(self.pair(), original)

    def test_unready_index_blocks_new_ironman_and_retries(self):
        self.invoke("seed")
        self.invoke("interrupt")
        self.invoke("busy-index")
        self.assertTrue((self.stage / "pending").exists())
        self.invoke("scan")

    def test_deleted_interrupted_save_does_not_reappear(self):
        for operation in ("delete", "delete-all"):
            with self.subTest(operation=operation):
                self.invoke("seed")
                self.invoke("interrupt")
                self.invoke(operation)
                self.invoke("scan-empty")

    def test_delayed_rescan_waits_for_backstory_browser_to_close(self):
        self.invoke("seed")
        original = self.pair()
        self.invoke("interrupt")
        self.invoke("backstory")
        self.assertEqual(self.pair(), original)


if __name__ == "__main__":
    unittest.main()
