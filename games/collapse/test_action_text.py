"""Regression tests for the Chinese action labels, conditions and event arguments."""
import re
import sys
import unittest
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import collapse


def fields(text):
    return Counter(re.findall(r'\{(\d+)\}', text))


class ActionTextTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = collapse.load_edits()

    def test_ministry_action_and_hover_refer_to_the_same_policy(self):
        table = self.rows['system_text']
        # The policy array and its description array start 280 rows apart.
        pairs = {
            545: ('军用采购', '政府采购'),
            558: ('免税经商', '免税区域'),
            578: ('采购国产装备', '本国产品'),
            584: ('更换军装', '新制服'),
            626: ('公交票价优惠', '公共交通票价优惠'),
            630: ('鼓励企业行善', '企业家'),
            644: ('建设商务中心', '商务中心'),
            674: ('改革教育', '教育应按新标准改革'),
            683: ('取消免费高教', '取消这项制度'),
            686: ('限制极右出版物', '限制极右翼出版物'),
            687: ('限制极左出版物', '限制极左翼出版物'),
            697: ('发展建筑业', '发展建筑业'),
            746: ('保护本国企业', '限制外资'),
        }
        for row, (button, subject) in pairs.items():
            with self.subTest(row=row):
                self.assertEqual(table[row], button)
                tooltip = table[row + 280]
                self.assertIn(subject, tooltip.split('{0}', 1)[0])
                rendered = tooltip.format('\n', '已启用', '预算充足')
                self.assertIn('当前状态：</color>已启用', rendered)
                self.assertEqual('所需条件：</color>预算充足' in rendered, row != 746)

    def test_alliance_condition_and_status_use_distinct_arguments(self):
        gui = self.rows['gui_text']
        self.assertRegex(gui[2151], r'至少\s*3\s*个邻国.*稳定.*伙伴')
        # Here {0} is the status; in the university fee popup it is a line break.
        self.assertEqual(gui[2152].format('已建立', '邻国条件', '\n').splitlines()[-3:],
                         ['<color=#8B0000>当前状态：</color>已建立',
                          '<color=#8B0000>所需条件：</color>', '邻国条件'])
        for row, child in ((2141, '儿子'), (2142, '女儿')):
            tooltip = gui[row].format('\n', '余额充足', '大学甲', 2, 5)
            self.assertIn(child+'就读于<color=#0027FF>大学甲</color>', tooltip)
            self.assertIn('2/3。每门学费：5 千镑', tooltip)
            self.assertTrue(tooltip.endswith('条件：\n余额充足'))

    def test_completed_propaganda_campaign_names_issuer_and_region(self):
        events = self.rows['event_text']
        for row in (6827, 6828):
            text = events[row].format('\n', '甲党', '工业区', '乙党')
            self.assertTrue(text.startswith('甲党在工业区'))
            self.assertIn('分发甲党的报纸和传单', text)
            self.assertIn('甲党成功挤走了其他政党', text)
            self.assertEqual('乙党' in text, row == 6828)
            self.assertEqual(text.count('\n'), 2)
        self.assertIn('调查乙党的活动', events[4062].format('\n', '经济部', '乙党'))

    def test_minister_and_party_leader_are_distinct_event_arguments(self):
        events = self.rows['event_text']
        for row in range(7432, 7439):
            self.assertTrue(events[row].format('\n').endswith('领导人\n被俘！'))
        for row, outcome in ((7439, '支付赎金'), (7440, '联系内务部'),
                             (7489, '支付赎金'), (7490, '联系内务部')):
            with self.subTest(row=row):
                title = '甲党' if row < 7489 else '经济部长'
                text = events[row].format('\n', title, 50)
                self.assertIn('<color=#CA0000>50</color>红第纳尔', text)
                self.assertIn(outcome, text.split('\n')[1])
                self.assertEqual('甲党领导人' in text, row < 7489)
                self.assertEqual('经济部长' in text, row >= 7489)
                self.assertEqual('党主席' in text, False)
        self.assertEqual(events[7494], '经济部长')
        self.assertEqual(events[7497], '农业部长')
        for row in range(7457, 7485):
            expected = '本党' if row < 7471 else '盟党'
            self.assertIn(expected, events[row])

    def test_threshold_steps_are_percentage_points(self):
        system = self.rows['system_text']
        for row, direction, limit in ((1070, '提高', '上限：50%'),
                                      (1071, '降低', '下限：30%'),
                                      (1072, '提高', '上限：50%')):
            text = system[row].format('\n', 40, '已达到条件')
            self.assertIn(direction + ' 10 个百分点', text)
            self.assertEqual(text.splitlines()[1:], ['当前门槛：40%', limit, '已达到条件'])

    def test_election_returns_and_clash_variants(self):
        events = self.rows['event_text']
        self.assertEqual(fields(events[594]), Counter({**{str(i): 1 for i in range(15)}, '15': 18}))
        for row in (2144, 2145):
            self.assertEqual(fields(events[row]), Counter({'0': 1, '2': 2, '3': 1, '4': 4}))
            rendered = events[row].format('南部地区', None, '甲族', '乙族', '\n')
            self.assertEqual(rendered.count('\n'), 4)
            self.assertIn('立即展开谈判', rendered.splitlines()[-1])
            self.assertEqual('修改' in rendered.splitlines()[-1], row == 2144)


if __name__ == '__main__':
    unittest.main()
