"""Regression cases for text fragments embedded by GetEventAnswers."""
import unittest

import audit
import collapse


class EventHints(unittest.TestCase):
    def test_conditions_have_no_nested_fields(self):
        rows = collapse.load_edits()['event_text']
        # Event 1, fourth option. The labels and the conjunction are already
        # arguments, so a field in any one of them would remain visible.
        hint = '{1}: -10{0}{2}{0}{3} -10'.format('\n', '经济部预算', rows[2831], '社会发展部预算')
        self.assertEqual(hint.splitlines(), ['经济部预算: -10', '或', '社会发展部预算 -10'])
        for line in (5364, 5379, 5394, 5403, 5414, 5426, 5436):
            with self.subTest(line=line):
                self.assertNotRegex(rows[line], r'[{}]')
                self.assertIn('关系', rows[line])
                self.assertIn('影响力', rows[line])

    def test_nested_and_substituted_fields_are_distinct(self):
        rows = [('OK', 'OK', '好', '好')] * 2830
        rows[0] = ('{0}', '{0}', '{0}', '{0}')
        rows.append(('OR', 'ИЛИ', '民选或者{0}', '民选或者{0}'))
        _, hard, candidates = audit.audit([('event_text', rows)])
        target = [('event_text', 2831)]
        self.assertEqual(hard['unexpanded_event_hint'], target)
        self.assertEqual(candidates['extra_zero_field'], target)

    def test_an_event_detail_still_inserts_the_region(self):
        value = collapse.load_edits()['event_text'][7274]
        self.assertEqual(value.format('\n', '东部地区'),
                         '东部地区的军火库不久前发生爆炸。现场的地雷和瓦砾现已全部清除。')

    def test_disabled_budget_options_explain_the_shortfall(self):
        events = collapse.load_edits()['event_text']
        for line in [*range(2813, 2823), 4857, 4858, 4879, 4880, 5990]:
            with self.subTest(line=line):
                self.assertIn('不足所需的一半', events[line])
        # The economy ministry uses a different message for each condition.
        self.assertEqual(events[2808], '须由本党掌管经济部')
        self.assertEqual(events[2817], '经济部预算不足所需的一半')


if __name__ == '__main__':
    unittest.main()
