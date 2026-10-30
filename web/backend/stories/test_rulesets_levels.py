"""Ruleset engine with levels, character axes and choices; an invented mini-system, never commercial rules."""
from django.test import SimpleTestCase

from . import rulesets
from .domain_errors import DomainError


class FormulaComparisonTests(SimpleTestCase):
    def test_comparisons_and_boolean_ops_yield_1_or_0(self):
        tree, names = rulesets.parse("SIL >= 13 and WIE >= 13", "x")
        self.assertEqual(names, {"SIL", "WIE"})
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 13, "WIE": 14}), 1)
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 12, "WIE": 14}), 0)
        tree, _ = rulesets.parse("SIL < 3 or SIL == 9", "x")
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 9}), 1)
        tree, _ = rulesets.parse("1 < SIL < 3", "x")
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 2}), 1)
        self.assertEqual(rulesets.evaluate(tree, {"SIL": 3}), 0)

    def test_still_rejected(self):
        for source in ["True", "not SIL", "SIL is 1", "SIL in [1]", "SIL < 'a'"]:
            with self.subTest(source=source), self.assertRaises(DomainError):
                rulesets.parse(source, "x")

    def test_dice_keep(self):
        self.assertEqual(rulesets.dice_keep("4d6kh3", "x"), (4, 6, 3))
        self.assertEqual(rulesets.dice_keep("3d6", "x"), (3, 6, 3))
        for bad in ["4d6kh5", "4d6kh0", "4d6/2", "d6", "4d6kh", 7]:
            with self.subTest(bad=bad), self.assertRaises(DomainError):
                rulesets.dice_keep(bad, "x")
        with self.assertRaises(DomainError):
            rulesets.dice("4d6kh3", "x")
