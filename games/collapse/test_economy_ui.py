"""Regression checks for ownership reform, budget transfers and party distrust."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import collapse


class EconomyInterfaceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = collapse.load_edits()

    def test_ownership_wizard(self):
        gui = self.rows['gui_text']
        self.assertEqual([gui[i] for i in range(2270, 2275)],
                         ['操作', '转出', '出售', '转入', '转为'])
        self.assertIn('混合所有制企业', gui[2280])
        self.assertIn('有偿收购', gui[2281])
        self.assertIn('无偿征收', gui[2282])
        self.assertIn('7 家企业', gui[2292].format(7, '国有部门'))
        self.assertIn('混合制下须兼有两职', gui[2262])
        self.assertIn('低于 50%', gui[2310])
        self.assertIn('25%、50% 或 100%', gui[2311])
        self.assertIn('不得超过 100', gui[2311])

    def test_nationalization_and_neutral_sector_modifier(self):
        gui = self.rows['gui_text']
        self.assertEqual(gui[2642].format(30, 50), '国有占比 30% → 目标 50%')
        self.assertIn('国有占比调至 70%', gui[2662].format('能源', 70))
        neutral = gui[2664].format('铁路')
        for meaning in ('铁路', '国有和私有占比各为 50%', '不产生每月效果'):
            self.assertIn(meaning, neutral)

    def test_budget_transfer_direction_and_surplus_arguments(self):
        gui = self.rows['gui_text']
        self.assertEqual(gui[2331], '委员会')
        self.assertEqual(gui[2380], '商务委员会')
        self.assertIn('盈余为 200', gui[2338].format(25, 200))
        self.assertIn('25% 将拨给所选部门', gui[2338].format(25, 200))
        self.assertEqual(gui[2339].format('经济部', 50), '经济部预算：+50')
        self.assertEqual(gui[2340].format(50), '总理预算：−50')
        self.assertIn('盈余为 200', gui[2385].format(25, 200))
        self.assertIn('25% 将调入总理预算', gui[2385].format(25, 200))
        self.assertEqual(gui[2386].format(50), '总理预算：+50')
        self.assertEqual(gui[2387].format('经济部', 50), '经济部预算：−50')

    def test_party_distrust_and_unconstitutional_points(self):
        for line in (self.rows['gui_text'][1825], self.rows['system_text'][1496]):
            self.assertIn('孤立者', line)
            self.assertIn('不信任值增加 1 点', line)
            self.assertIn('违宪点数减少 1 点', line)


if __name__ == '__main__':
    unittest.main()
