import unittest
from decimal import Decimal

from mathoperation.division import divide


class DivisionFunctionTest(unittest.TestCase):
    def test_divides_numbers_as_decimal(self) -> None:
        result = divide(10, 4)
        self.assertEqual(Decimal("2.5"), result)

    def test_zero_denominator_raises(self) -> None:
        with self.assertRaises(ZeroDivisionError):
            divide(1, 0)

    def test_non_numeric_input_raises_value_error(self) -> None:
        with self.assertRaises(ValueError):
            divide("a", 3)


if __name__ == "__main__":
    unittest.main()
