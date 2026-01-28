from decimal import Decimal

def divide(numerator, denominator):
    if denominator == 0:
        raise ZeroDivisionError("Denominator must not be zero.")
    return Decimal(numerator) / Decimal(denominator)
