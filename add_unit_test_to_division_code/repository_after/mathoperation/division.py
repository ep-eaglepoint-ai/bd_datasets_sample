from __future__ import annotations

from decimal import Decimal, InvalidOperation
from typing import Union

Number = Union[int, float, Decimal]


def divide(numerator: Number, denominator: Number) -> Decimal:
    """Safely divide two numbers returning a Decimal result."""
    try:
        num = Decimal(numerator)
        den = Decimal(denominator)
    except (InvalidOperation, ValueError, TypeError) as exc:
        raise ValueError("Inputs must be numeric.") from exc

    if den == 0:
        raise ZeroDivisionError("Denominator must not be zero.")

    return (num / den).normalize()
