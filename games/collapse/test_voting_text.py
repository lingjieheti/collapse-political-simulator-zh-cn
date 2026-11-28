"""Check the Chinese templates against the actual arguments of the voting screen."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import collapse


class VotingTextTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        tables = collapse.load_edits()
        cls.gui = tables['gui_text']
        cls.events = tables['event_text']

    def test_forecast_marks_are_not_counts_of_deputies(self):
        # RepaintDeputiesNeeded supplies an approval symbol and a separate percentage.
        template = self.gui[1564].replace('{4:F1}', '{4:.1f}')
        for threshold, support, yes in ((50.1, 50.0, '☐'), (66, 72.4, '☑')):
            with self.subTest(threshold=threshold):
                lines = template.format('\n', threshold, 3, yes, support, '☐').splitlines()
                self.assertEqual(lines, [f'通过门槛：{threshold}%', '修改法案：3项',
                                         f'支持率：{support:.1f}% {yes}', '预计否决：☐'])

    def test_draft_actions_and_the_proposing_party(self):
        self.assertEqual(self.gui[330], '提交表决')
        self.assertEqual(self.gui[1609], '撤回修改')
        self.assertEqual(self.gui[327], '提案方')
        self.assertIn('提案党支付', self.gui[1557])
        self.assertIn('按法案通过计算', self.gui[1557])
        self.assertNotRegex(self.gui[1557], r'\d')
        for selected in ('自由党', '-'):
            own = self.gui[796].format('\n', 24, 0, selected, 0)
            self.assertIn('本党席位：24', own)
            self.assertIn('指定政党：' + selected, own)
            self.assertIn('如有指定', own)
        other = self.gui[575].format('\n', 24, 40, '-', 2)
        self.assertIn('剩余黑材料：40', other)
        self.assertIn('已争取议员：2', other)

    def test_estimates_and_actual_charges_keep_their_units_and_payer(self):
        card = self.gui[1907].format('\n', 450, 60.6, 10, 30.3)
        self.assertEqual(card.splitlines(), ['余额/费用', '党内资金', '450 / 60.6',
                                             '行政资源', '10 / 30.3'])
        for row, outcome, admin, funds in ((325, '法案通过', 30.3, 60.6),
                                           (326, '法案未通过', 29.7, 59.4),
                                           (2243, '总统否决法案', 29.7, 59.4)):
            with self.subTest(row=row):
                text = self.gui[row].format(1, '\n', admin, funds)
                self.assertIn(f'{outcome}（修改1项）', text)
                self.assertIn(f'提案党支出：行政资源{admin}，党内资金{funds}', text)

    def test_veto_types_and_affordability(self):
        for row in (2241, 2242):
            self.assertIn('100.0行政资源', self.gui[row])
        self.assertIn('100行政资源', self.gui[2708].format(100))
        # OnButtonEnter uses row 2692 for type 2, row 2693 for type 1.
        self.assertIn('66%', self.gui[2692])
        self.assertIn('选举前最后三个月', self.gui[2692])
        self.assertIn('66%', self.gui[2693])
        self.assertNotIn('选举', self.gui[2693])
        self.assertIn('绝对否决权', self.gui[2694])

    def test_currency_and_budget_views_name_the_actual_account(self):
        for button, description, verb in ((677, 712, '买入'), (678, 713, '卖出')):
            self.assertEqual(self.gui[button], verb + '本币')
            # PartyController's native hover stops at the first format field.
            title, _, details = self.gui[description].partition('{0}')
            self.assertIn(verb + '10单位本币', title)
            self.assertTrue(title.endswith('。'))
            self.assertIn('当前汇率：12。本币持有量：20。', details.format('\n', '', 12, 20))
        for row in (28, 155, 744):
            self.assertEqual(self.gui[row], '党内资金')
        self.assertIn('党内资金：{8}', self.gui[306])
        self.assertIn('小企业营商环境', self.gui[308])
        self.assertEqual(self.gui[1625].format('\n', '-'), '合作政党：-')
        lines = self.gui[2146].format('\n', '国家预算', 11, 22, 33, 44, 55, -50, 77).splitlines()
        self.assertEqual(lines[-2:], ['外交政策收支：-50', '部委收支：77'])

    def test_cabinet_title_in_event_227(self):
        self.assertEqual(self.events[4277], '分配内阁职务')


if __name__ == '__main__':
    unittest.main()
