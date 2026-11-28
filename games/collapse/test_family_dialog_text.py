"""Family event text must be usable when the English and Workshop slots are empty."""
import unittest

import collapse


class FamilyDialogTextTests(unittest.TestCase):
    def test_every_russian_only_family_row_has_an_explicit_translation(self):
        edits = collapse.load_edits()['event_text']
        missing_from_workshop = set(range(7601, 7801)) | set(range(7802, 7922))
        self.assertEqual(len(missing_from_workshop), 320)
        for row in missing_from_workshop:
            with self.subTest(row=row):
                self.assertTrue(collapse.family_dialog_row('event_text', row))
                self.assertTrue(edits[row].strip())

    def test_family_names_match_in_letters_and_place_names(self):
        edits = collapse.load_edits()['event_text']
        # Wives and daughters use the same set of names, as do husbands and sons.
        for parent, child, place, count in ((7654, 7901, 7704, 21), (7675, 7881, 7735, 20)):
            for i in range(count):
                with self.subTest(parent=parent+i):
                    name = edits[place+i]
                    self.assertTrue(name)
                    self.assertEqual(edits[parent+i].split('{0}')[-1].rstrip('。'), name)
                    self.assertEqual(edits[child+i].split('{0}')[-1].rstrip('。'), name)

    def test_birthday_message_uses_chinese_month_then_day(self):
        edits = collapse.load_edits()
        march = edits['gui_text'][2021]
        self.assertEqual(march, '三月')
        spouse = edits['event_text'][7798].format('\n', '妮娜', '主席', march, 21, 41)
        children = edits['event_text'][7799].format('\n', '妮娜', '帕维尔', '主席', march, 18, 12)
        for message, date, age in ((spouse, '三月21日', '41'), (children, '三月18日', '12')):
            self.assertIn(date, message)
            self.assertIn(age, message)
            self.assertNotIn('{', message)


if __name__ == '__main__':
    unittest.main()
