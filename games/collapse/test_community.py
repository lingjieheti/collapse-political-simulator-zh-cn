"""Regression checks for the Build 24949409 candidate ballot and coalition tooltips."""
import re
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit
import collapse


class CommunityRegressions(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = collapse.load_edits()

    def test_candidate_ballots_match_officeholders(self):
        events = self.rows['event_text']
        interface = self.rows['gui_text']
        traits = set()
        for index in range(20):
            ballot_line = (8298 if index < 10 else 8302) + index
            with self.subTest(ballot_line=ballot_line):
                role = '总统' if index < 10 else '总理'
                heading = audit.plain(interface[760 + index].split('|', 1)[0])
                trait = re.fullmatch(role + r'（(.+?)）', heading)[1]
                self.assertEqual(events[ballot_line], f'提名候选人（{trait}）')
                # The full asset audit also checks the report arguments that
                # already had the correct value in the Workshop base.
                if 8325 + index in events:
                    self.assertEqual(events[8325 + index], trait)
                traits.add(trait)
        self.assertEqual(len(traits), 20)
        self.assertIn('贫困阶层支持率 +0.4', interface[778])
        self.assertIn('中间阶层支持率 +0.4', interface[778])
        self.assertEqual(interface[761].count('{0}'), 2)

    def test_coalition_action_boundaries(self):
        rows = self.rows['gui_text']
        self.assertIn('本党团结度 −5.0', rows[1655])
        self.assertIn('本党团结度 −1.0', rows[1656])
        failed_bill = rows[1657].split('表决未通过：')[1]
        self.assertIn('两党关系 −15.0', failed_bill)
        self.assertIn('本党对盟党的影响力 −15.0', failed_bill)
        enough, short = rows[1658].split('只有 5 名线人或更少')
        self.assertIn('至少有 6 名线人', enough)
        self.assertIn('消耗 5 名线人', enough)
        self.assertIn('本党对盟党的影响力 −3.0', short)
        solvent, insolvent = rows[1668].split('若任一方党内资金为零或负数')
        self.assertIn('支持对方的阶层激进度 −0.5', solvent)
        self.assertIn('所有阶层激进度 +1.0', insolvent)
        self.assertIn('高于 25.0', rows[1673])
        self.assertIn('两党团结度各设为 100.0', rows[1673])
        self.assertIn('不信任值清零', rows[1673])


if __name__ == '__main__':
    unittest.main()
