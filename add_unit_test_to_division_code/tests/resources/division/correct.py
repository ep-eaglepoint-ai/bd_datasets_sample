from decimal import Decimal, InvalidOperation

def divide(numerator, denominator):
    try:
        num = Decimal(numerator)
        den = Decimal(denominator)
    except (InvalidOperation, ValueError, TypeError) as exc:
        raise ValueError("Inputs must be numeric.") from exc
    if den == 0:
        raise ZeroDivisionError("Denominator must not be zero.")
    return (num / den).normalize()
