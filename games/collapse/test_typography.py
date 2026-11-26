import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import collapse
from layout import wrap_chinese


class ChineseTypography(unittest.TestCase):
    def test_digit_padding_is_not_a_word_break(self):
        cleaned = collapse.clean_line('event_text', 119, '1 1 月，4 0 2 0 万票。',
                                      'In November, 40.2 million votes.')
        self.assertEqual(cleaned, '11 月，4020 万票。')
        groups = '710 609 175 188 282 000'
        self.assertEqual(collapse.clean_line('event_text', 2041, groups, ''), groups)

    def test_numeric_and_western_spacing(self):
        result = wrap_chinese('效果： - 0.5，增长 40 %。|A New Deal|{2:F1}<b>条件</b>')
        self.assertIn('-0.5', result)
        self.assertIn('40%', result)
        self.assertIn('A \u200aNew \u200aDeal', result)
        self.assertIn('|{2:F1}<b>条件</b>', result)
        self.assertEqual(wrap_chinese('   John Smith'), '   John \u200aSmith')

    def test_narrow_labels_and_tooltip_breaks(self):
        edits = collapse.load_edits()
        self.assertEqual(edits['gui_text'][1533], '暂停后自动继续')
        self.assertEqual(edits['gui_text'][1610], '国内生产总值')
        for n in (995, 1460):
            self.assertIn('</color>|', edits['gui_text'][n])
        for n in (580, 581, 582):
            # The map removes an ASCII colon and appends its own.
            self.assertIn(':</color>', edits['gui_text'][n])
        self.assertIn('{1} 千人', edits['gui_text'][2715])
        self.assertIn('{2} 千人', edits['gui_text'][2715])
        self.assertIn('{1}/{2}', edits['gui_text'][2689])
        for n in (1429, 1432, 1438, 1441, 1486, 1491, 1495, 1500, 1504):
            row = edits['system_text'][n]
            self.assertEqual(row.count('|'), 3)
            self.assertTrue(row.split('|')[0].endswith('特殊修正</color>'))
        self.assertEqual(edits['system_text'][467].count('|'), 5)


if __name__ == '__main__':
    unittest.main()
