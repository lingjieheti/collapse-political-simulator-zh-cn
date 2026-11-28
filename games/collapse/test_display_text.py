"""Check the contracts between the game's call sites and the release translations."""
from pathlib import Path
import hashlib
import re
import unittest

import collapse


class DisplayTextTests(unittest.TestCase):
    def test_program_phrases_have_unique_verified_addresses(self):
        lines = (Path(__file__).parent / "display_literals.tsv").read_text("utf8").splitlines()
        sites = set()
        character = 0
        rating = ("CountryController", "OneShotEffectsForActionButton", 71)
        for line in lines:
            controller, method, ordinal, native_digest, replacement = line.split("\t")
            key = controller, method, int(ordinal)
            self.assertNotIn(key, sites)
            sites.add(key)
            self.assertRegex(native_digest, r"^[a-f0-9]{64}$")
            if key == rating:
                # The effect builder adds the positive sign itself for a benefit.
                self.assertEqual(native_digest, hashlib.sha256(b"+30").hexdigest())
                self.assertEqual("+" + replacement, "+30")
            else:
                self.assertRegex(replacement, r"[一-鿿]")
            if controller == "StatisticsPersonalityController":
                # Numeric masks use .NET syntax. The original Unity methods exercise
                # the masks; this offline check validates braces and argument IDs.
                re.sub(r"\{(\d+):[^{}]+\}", r"{\1}", replacement).format(*[1.3] * 5)
                character += 1
        self.assertEqual(35, character)
        self.assertIn(rating, sites)
        self.assertEqual(57, len(sites))

    def test_election_seats_and_excommunications(self):
        event = collapse.load_edits()["event_text"]
        for indices in (range(5963, 5969), range(6022, 6028)):
            recipients = set()
            for row in indices:
                text = event[row]
                self.assertIn("议席", text)
                parties = re.findall(r"<color=([^>]+)>([^<]+)</color>", text)
                self.assertEqual(2, len(parties))
                self.assertNotEqual(parties[0], parties[1])
                recipients.add(parties[1])
            self.assertEqual(6, len(recipients))
        for offset in range(7):
            choice = event[7311 + offset]
            result = event[7348 + offset]
            self.assertIn("施以绝罚", choice)
            self.assertIn("遭教会绝罚", result)
            self.assertEqual(re.search(r"<color=.*?</color>", choice)[0],
                             re.search(r"<color=.*?</color>", result)[0])

    def test_family_occupations_and_upload(self):
        gui = collapse.load_edits()["gui_text"]
        self.assertIn("任职", gui[2422])
        self.assertIn("丑闻", gui[2434])
        self.assertIn("子女", gui[2437])
        self.assertIn("赴俄务工", gui[941])
        self.assertEqual("Steam 上传进度：11/11", gui[1915].format(11))


if __name__ == "__main__":
    unittest.main()
