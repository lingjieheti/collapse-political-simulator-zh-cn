"""Regression checks for the fixed-size Unity panels in Steam build 24949409."""
import hashlib
import os
from pathlib import Path
import struct
import unittest

import visual_assets as visual


def synthetic_scene(name):
    table, mesh_type, transform_type = visual.SCENE_TABLES[name]
    groups = visual.MESH_SIZES[name]
    transforms = visual.TRANSFORM_Y.get(name, ())
    ids = [i for _, _, group in (*groups, *transforms) for i in group]
    calendar = (*visual.DATE_DIGITS, 587, 595, 1060) if name == 'level5' else ()
    ids.extend(calendar)
    start = (table + max(ids) * 24 + 15) & ~15
    source = bytearray([0xAC]) * (start + len(ids) * 100)
    struct.pack_into('>Q', source, 32, start)
    expected = bytearray(source)
    relative = 0
    for is_mesh, items in ((True, groups), (False, transforms)):
        for old, new, group in items:
            for i in group:
                type_index = mesh_type if is_mesh else transform_type
                obj = start + relative
                entry = table + (i - 1) * 24
                for data in (source, expected):
                    struct.pack_into('<qqIi', data, entry, i, relative, 100, type_index)
                if is_mesh:
                    text = '场景'.encode() if i % 2 else b''
                    for data in (source, expected):
                        struct.pack_into('<I', data, obj + 12, len(text))
                        data[obj + 16:obj + 16 + len(text)] = text
                    field = obj + (28 if text else 20)
                    if name == 'level2' and i == 283:
                        struct.pack_into('<H', source, field + 8, 1)
                        struct.pack_into('<H', expected, field + 8, 4)
                    if name == 'level6' and i == 628:
                        struct.pack_into('<f', source, field + 4, 1)
                        struct.pack_into('<f', expected, field + 4, .82)
                else:
                    field = obj + 32
                struct.pack_into('<f', source, field, old)
                struct.pack_into('<f', expected, field, new)
                relative += 100
    for i in calendar:
        entry = table + (i - 1) * 24
        obj = start + relative
        for data in (source, expected):
            kind = mesh_type if i == 1060 else transform_type
            struct.pack_into('<qqIi', data, entry, i, relative, 100, kind)
        if i == 1060:
            source[obj+12:obj+24] = b'\x06\0\0\0.    .\0\0'
            expected[obj+12:obj+24] = b'\x08\0\0\0.      .'
        elif i == 587:
            struct.pack_into('<f', source, obj+28, -17.663)
            struct.pack_into('<f', expected, obj+28, -17.763)
            struct.pack_into('<f', source, obj+40, 1)
            struct.pack_into('<f', expected, obj+40, 1.1)
        else:
            struct.pack_into('<ff', source, obj+40, 1, 1)
            struct.pack_into('<ff', expected, obj+40, 1.05 if i == 595 else .355, .72)
        relative += 100
    return source, expected


def size_for(scene, object_id):
    return next(new for _, new, ids in visual.MESH_SIZES[scene] if object_id in ids)


class SceneLayoutTests(unittest.TestCase):
    def test_keeps_other_serialized_fields_intact(self):
        for name in visual.SCENE_TABLES:
            with self.subTest(scene=name):
                source, expected = synthetic_scene(name)
                visual.fit_scene_controls(name, source)
                self.assertEqual(source, expected)

    def test_rejects_wrong_object_id_type_and_size(self):
        scene = 'level2'
        table, mesh_type, _ = visual.SCENE_TABLES[scene]
        mesh_id = 283
        entry = table + 24 * (mesh_id - 1)
        source, _ = synthetic_scene(scene)
        start = visual.object_offset(scene, source, mesh_id, mesh_type)
        for offset in (entry, entry + 20, start + 28):
            with self.subTest(offset=offset):
                broken = source.copy()
                broken[offset] ^= 1
                with self.assertRaises(ValueError):
                    visual.fit_scene_controls(scene, broken)

    def test_save_table_and_game_panels_have_room(self):
        # Advances of dd.MM.yy HH:mm and dd.MM.yyyy in the patched font.
        # Unity's fallback 40-point font uses 4 world units per characterSize.
        self.assertLess(6.464 * 4 * size_for('level6', 589), 2.50 - .20)
        self.assertLess(4.996 * 4 * size_for('level6', 612), 2.30 - .20)
        # The baked dialog texture has a 50-pixel gap at 100 pixels per unit.
        # The counter updates on a second line, even for an empty name.
        caption_em = 7 * size_for('level6', 628)
        self.assertLess(caption_em * (1 + .82), .50 - .05)
        self.assertLess(7 * 6 * size_for('level2', 283), 2.65 - .15)
        self.assertLess(4 * 12 * .45 * size_for('level5', 988), 2.376 - .15)
        self.assertLess(6 * 8 * .45 * size_for('level12', 408), 1.4535 - .13)
        self.assertLess(4 * 6 * .45 * size_for('level13', 1107), 2.85 - .20)
        self.assertLess(6 * 12 * .45 * size_for('level13', 1135), 2.62 - .20)
        self.assertLess(12 * .45 * size_for('level13', 1158), .171)

    def test_rolling_digits_stay_separate_at_both_resolutions(self):
        self.assertEqual(len(set(visual.DATE_DIGITS)), 3 * 8)
        # At 90 points, 0.09 characterSize, the digit with the largest ink
        # width has a 0.504em outline. Adjacent centres are 0.2 local units.
        for pixels_per_unit in (72, 108):
            cell = .2 * .46232 * 1.10
            ink = 9 * .09 * .46232 * 1.10 * .355 * .504
            self.assertGreater((cell - ink) * pixels_per_unit, 1.9)

    @unittest.skipUnless(os.environ.get('COLLAPSE_TEST_GAME'), 'set COLLAPSE_TEST_GAME for installed game data')
    def test_installed_scenes_match_pinned_source_and_candidate(self):
        game = Path(os.environ['COLLAPSE_TEST_GAME'])
        for scene in visual.SCENE_TABLES:
            with self.subTest(scene=scene):
                original = (game / '.collapse-zh-24949409' / (scene + '.original')).read_bytes()
                self.assertEqual(hashlib.sha256(original).hexdigest(), visual.VISUAL_HASHES[scene])
                candidate = visual.build_visual(scene, original, {})
                self.assertEqual(candidate, (game / 'PoliticalSimulator_Data' / scene).read_bytes())


if __name__ == '__main__':
    unittest.main()
