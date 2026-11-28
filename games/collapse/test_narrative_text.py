"""Regression tests for political event choices, formatted results and long prose."""
from pathlib import Path
import unittest

import collapse


class NarrativeTextTests(unittest.TestCase):
    def test_political_choices_and_results(self):
        rows = collapse.load_edits()['event_text']
        self.assertIn('惩治煽动仇恨', rows[5992])
        self.assertIn('反对打击民族主义', rows[6008])
        self.assertIn('支持打击民族主义', rows[6009])
        self.assertIn('至少掌管四个政府职位', rows[6012])
        for first, topic in ((6002, '刑法'), (6004, '宪法')):
            for row, outcome in ((first, '通过'), (first + 1, '否决')):
                result = rows[row].format('\n', '政党甲', '内阁声明', 62)
                self.assertIn(topic, result)
                self.assertIn('62票' + outcome, result)
        arguments = ('\n', '政党甲', '政党乙', '政党丙', 62, 38, '禁令', '议席', '政府职位')
        for row in (6018, 6039):
            result = rows[row].format(*arguments)
            self.assertTrue(result.endswith('禁令\n议席\n政府职位'))
            self.assertEqual('62' in result, row == 6039)
        for row in range(6029, 6035):
            self.assertIn('下次选举', rows[row])
        self.assertIn('继续打击民族主义', rows[6041])
        for row in (4688, 6045, 6047):
            self.assertIn('宪法法院', rows[row])

    def test_fragment_positions_and_unique_source(self):
        changes = Path(collapse.__file__).with_name('narrative_fragments.tsv')
        rows = [''] * collapse.TABLES['event_text'][2]
        expected = list(rows)
        seen = set()
        for line in changes.read_text('utf-8').splitlines():
            num, old, new = line.split('\t')
            index = int(num) - 1
            self.assertNotIn((index, old), seen)
            seen.add((index, old))
            # The pre-wrap upstream text may put a space between every glyph.
            rows[index] += '<i>保留</i>' + ' '.join(old) + '{0}'
            expected[index] += '<i>保留</i>' + new + '{0}'
        original = list(rows)
        self.assertGreater(len(seen), 0)
        collapse.revise_event_fragments(rows)
        self.assertEqual(rows, expected)
        for bad in ('', original[457] * 2):
            invalid = original.copy()
            invalid[457] = bad
            with self.assertRaisesRegex(ValueError, '458'):
                collapse.revise_event_fragments(invalid)


if __name__ == '__main__':
    unittest.main()
