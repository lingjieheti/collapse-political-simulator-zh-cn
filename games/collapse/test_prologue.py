"""Regression check for the biography heading in Steam build 24949409."""
import hashlib
import os
from pathlib import Path
import struct
import unittest

import visual_assets


class PrologueTitleTests(unittest.TestCase):
    # Back/UI/MainText, TextMesh 111. CI uses synthetic bytes around the field.
    size_field = 14232
    position_field = 8620

    def test_only_the_title_scale_and_vertical_position_change(self):
        source = bytearray([0xAC]) * (self.size_field + 32)
        struct.pack_into('<ff', source, self.size_field, 0.32, 1.0)
        struct.pack_into('<fff', source, self.position_field, -13.05, 6.05, -1.0)
        expected = source.copy()
        struct.pack_into('<f', expected, self.size_field, 0.25)
        struct.pack_into('<f', expected, self.position_field + 4, 6.11)
        self.assertIn('level15', visual_assets.VISUAL_HASHES)
        self.assertEqual(visual_assets.build_visual('level15', source, {}), expected)

    def test_unknown_title_layout_is_rejected(self):
        with self.assertRaisesRegex(ValueError, '背景故事标题'):
            visual_assets.build_visual('level15', bytes(self.size_field + 32), {})

    @unittest.skipUnless(os.environ.get('COLLAPSE_TEST_GAME'), 'set COLLAPSE_TEST_GAME to verify the installed scene')
    def test_installed_scene_matches_the_original_except_for_the_title(self):
        game = Path(os.environ['COLLAPSE_TEST_GAME'])
        original = (game / '.collapse-zh-24949409/level15.original').read_bytes()
        installed = (game / 'PoliticalSimulator_Data/level15').read_bytes()
        self.assertEqual(hashlib.sha256(original).hexdigest(), visual_assets.VISUAL_HASHES['level15'])
        expected = bytearray(original)
        struct.pack_into('<f', expected, self.size_field, 0.25)
        struct.pack_into('<f', expected, self.position_field + 4, 6.11)
        self.assertEqual(installed, expected)


if __name__ == '__main__':
    unittest.main()
