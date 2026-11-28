"""Check the text consumed by the diplomacy tabs and their action tooltips."""
import re
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import collapse
import audit


class DiplomacyTextTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        tables = collapse.load_edits()
        cls.gui = tables['gui_text']
        cls.system = tables['system_text']

    def test_short_actions_keep_the_full_subject_in_their_own_tooltip(self):
        # The first two tabs use a title and a description exactly 50 rows apart.
        cases = {
            835: '独联体议会间大会', 837: '独联体国家间银行', 838: '独联体外交部长理事会',
            841: '联合防空体系', 842: '俄白联盟国', 845: '独联体经济理事会',
            848: '欧亚经济共同体', 850: '独联体经济法院', 851: '退出欧亚经济共同体',
            857: '集安组织空军基地', 858: '圣彼得堡国际经济论坛', 860: '独联体技术援助计划',
            863: '欧洲－高加索－亚洲运输走廊', 866: '古阿姆组织', 867: '欧洲－大西洋伙伴关系理事会',
            869: '退出维谢格拉德集团', 870: '维谢格拉德集团', 871: '与欧盟签署联系协定',
            872: '国际维谢格拉德基金', 875: '欧盟提议与我国签署自由贸易协定',
        }
        for row, name in cases.items():
            with self.subTest(row=row):
                self.assertLessEqual(len(self.gui[row]), 6)
                self.assertNotRegex(self.gui[row], r'\s|#')
                self.assertIn(name, self.gui[row + 50])

    def test_world_actions_preserve_terms_and_game_format_arguments(self):
        cases = {
            948: ('物价', '012'), 950: ('本币结算', '012'),
            951: ('国际货币基金组织（IMF）', '012'), 952: ('削减社会支出', '12'),
            953: ('联合国教科文组织', '012'), 954: ('世界粮食计划署', '12'),
            1011: ('天然气', '12'), 1013: ('黑市', '1'),
            1573: ('仍可能遭受出于政治动机的制裁', '012'), 2466: ('国际经济合作组织（OIEC）', '12'),
        }
        for row, (term, arguments) in cases.items():
            with self.subTest(row=row):
                raw = self.gui[row]
                self.assertEqual(set(re.findall(r'\{(\d+)\}', raw)), set(arguments))
                text = raw.format('已参加', '条件甲', '\n')
                self.assertIn(term, text)
                self.assertTrue(text.endswith('条件甲'))
                self.assertEqual(text.count('<color='), text.count('</color>'))
        self.assertEqual(self.gui[942], '本币结算')
        self.assertEqual(self.gui[875], '对欧自贸协定')
        self.assertEqual(self.gui[2465], '重启经合计划')
        self.assertNotIn('容易', self.gui[1573])

    def test_authority_dependency_and_relations_are_separate_fields(self):
        self.assertEqual(self.system[1652], '外交主导方')
        self.assertEqual(self.system[1653], '无人主导')
        self.assertEqual(self.gui[936], '本党有外交决策权')
        self.assertEqual(self.gui[263] + ' 40', '我国依赖度： 40')
        self.assertEqual(self.gui[833].format(75), '对俄关系：75')
        self.assertEqual(self.gui[834].format(50), '对美关系：50')
        for row, who in ((992, '西方'), (993, '俄')):
            self.assertEqual(self.gui[row] + ' ≤ 75.0', f'对{who}依赖度上限 ≤ 75.0')

    def test_ministry_aid_name_displays_without_formatting(self):
        # Repaint writes system_text[1551] directly to action 20 for the social democrats.
        self.assertEqual(self.system[1552], '人道主义援助')
        empty = [('', '', '', '')] * 1551
        for result, expect_error in ((self.system[1552], False), ('人道主义{0}援助', True)):
            with self.subTest(result=result):
                rows = empty + [('Humanitarian{0}aid', 'Гуманитарная{0}помощь', '人道主义{0}援助', result)]
                _, errors, _ = audit.audit([('system_text', rows)])
                self.assertEqual(bool(errors), expect_error)

    def test_world_monthly_effects_use_relations_and_the_displayed_scale(self):
        # FormBudget uses US relations > 600 in native tenths. SomethingChanges
        # uses Russian relations > 400 and credits half the native integer.
        self.assertIn('对美关系高于60.0', self.gui[2583])
        self.assertIn('对美关系降至60.0或以下', self.gui[2597])
        self.assertIn('对俄关系高于40.0', self.gui[2572])
        self.assertIn('关系值的5倍（向下取整）', self.gui[2572])
        self.assertIn('对俄关系降至40.0或以下', self.gui[2596])


if __name__ == '__main__':
    unittest.main()
