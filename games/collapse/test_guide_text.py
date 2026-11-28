"""Check guide paragraphs and the formatted labels that players see in Build 24949409."""
import os
import re
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit
import collapse


class GuideTests(unittest.TestCase):
    def test_party_names_are_disambiguated_by_source(self):
        self.assertEqual(collapse.party_name('共 和 国 国 民 党', "National People's Party"), '国家人民党')
        self.assertEqual(collapse.party_name('中国国民党', 'The Kuomintang negotiated with the PNP.'), '中国国民党')

    def test_campaign_targets_use_same_unit_and_include_boundary(self):
        for n in (1330, 1331, 1332):
            text = collapse.load_edits()['system_text'][n].format(30, 21)
            self.assertIn('30</color>', text)
            self.assertIn('21</color> 或以下', text)

    def test_add_paragraph_without_renumbering_workshop_sections(self):
        rows = ['标题||第一节||末节']
        collapse.revise_guides(rows, {1: {1: '甲||乙', 2: '丙'}})
        self.assertEqual(rows, ['标题||甲||乙||丙'])
        with self.assertRaises(ValueError):
            collapse.revise_guides(rows, {1: {4: '越界'}})

    def test_satisfaction_report_preserves_all_regions_and_arguments(self):
        texts = collapse.load_edits()['system_text']
        for row in (1138, 1139, 1356, 1360, 1361):
            with self.subTest(row=row):
                line = texts[row]
                ids = sorted(int(x) for x in re.findall(r'\{(\d+)\}', line))
                self.assertEqual(ids, list(range(16)))
                rendered = line.format(32.3, 1, 2, 3, 4, 5, '甲部', '乙部', '丙部', '丁部', '戊部',
                                       '甲党', '乙党', '丙党', '丁党', '戊党')
                self.assertIn('上期全国平均值：32.3', rendered)
                self.assertIn('东部：<color=#646664>丁部 丁党</color>（4）', rendered)
        self.assertIn('贫困阶层', texts[1363])
        self.assertIn('中间阶层', texts[1362])

    @unittest.skipUnless(os.environ.get('COLLAPSE_TEST_GAME'), 'requires installed game resources')
    def test_installed_handbook_and_foundation_labels(self):
        game = Path(os.environ['COLLAPSE_TEST_GAME'])
        asset = (game/collapse.ASSET).read_bytes()
        def table(name):
            offset = collapse.TABLES[name][1]
            return collapse.object_data(asset, offset)[2].decode('utf-8-sig').splitlines()
        system = table('system_text')
        gui = table('gui_text')
        for n in (1255, 1256, 1257, 1258, 1259):
            with self.subTest(row=n):
                parts = system[n-1].split('||')
                self.assertEqual(len(parts), 11)
                self.assertIn('营商环境', audit.compact(parts[6]))
        social_tech = audit.compact(system[1271].split('||')[3])
        self.assertIn('贫困阶层激进度−0.2', social_tech)
        policy = audit.compact(system[1304].split('||')[10])
        self.assertIn('基金会', policy)
        self.assertIn('0.7', policy)
        self.assertIn('0.2', policy)
        self.assertNotIn('增加贫困阶层', policy)
        for s in (system[1542], gui[1867]):
            self.assertIn('基金会', audit.compact(s))
