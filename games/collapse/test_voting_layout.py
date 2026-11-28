"""Legislature colour contrast and optional native scene-14 geometry checks."""
import os
from pathlib import Path
import re
import unittest


def luminance(hex_rgb):
    rgb = [int(hex_rgb[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    linear = [channel / 12.92 if channel <= .04045 else
              ((channel + .055) / 1.055) ** 2.4 for channel in rgb]
    return sum(a * b for a, b in zip(linear, (.2126, .7152, .0722)))


class VotingLayout(unittest.TestCase):
    def test_forecasts_remain_readable_on_original_panel(self):
        code = Path(__file__).with_name('VotingLayout.cs').read_text(encoding='utf-8')
        colors = dict(re.findall(r'\.Replace\("<color=([^>]+)>", "<color=#([0-9A-F]{6})>"\)', code))
        self.assertEqual(set(colors), {'green', 'red', '#FF0027', '#FF008E', '#001DB0'})
        # Two native background colours sampled from the forecast panel.
        for label, foreground in colors.items():
            for background in ('6E323C', '753540'):
                with self.subTest(label=label, background=background):
                    self.assertGreaterEqual((luminance(foreground) + .05) /
                                            (luminance(background) + .05), 4.5)

    @unittest.skipUnless(os.getenv('COLLAPSE_VOTING_QA_DIR'), '需要原生表决页的逐行测量输出')
    def test_every_rendered_law_and_option_fits(self):
        directory = Path(os.environ['COLLAPSE_VOTING_QA_DIR'])
        for resolution in ('1280x720', '1920x1080'):
            found = set()
            for file in directory.glob(f'final-{resolution}-sweep*-rows.tsv'):
                for row in file.read_text(encoding='utf-8-sig').splitlines():
                    key, label, mesh, face, *help_button = row.split('\t')
                    left, top, right, bottom = map(float, face.split(','))
                    x0, y1, x1, y0 = map(float, mesh.split(','))
                    if help_button:
                        left = max(left, float(help_button[0].split(',')[2]))
                    with self.subTest(resolution=resolution, key=key, label=label):
                        self.assertTrue(label.strip())
                        self.assertGreater(min(x0 - left, top - y1, right - x1, y0 - bottom), .01)
                    found.add(key)
            expected = {f'law{i}' for i in range(34)} | {
                f'opt{1000 + law * 10 + option}' for law in range(34) for option in range(5)}
            self.assertEqual(found, expected)


if __name__ == '__main__':
    unittest.main()
