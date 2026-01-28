from decimal import Decimal

def divide(numerator, denominator):
    if denominator == 0:
        return Decimal("0")
    return Decimal(numerator) / Decimal(denominator)
