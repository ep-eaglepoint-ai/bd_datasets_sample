import math
import random
from datetime import datetime

import pytest

from repository_before.app.score import calc_score as calc_before
from repository_after.app.score import calc_score as calc_after


class FloatBad:
    def __float__(self):
        raise TypeError("no float")


class StrBad:
    def __str__(self):
        raise RuntimeError("bad str")


class StrNum:
    def __str__(self):
        return "2.5"


def _call(fn, events, user, now):
    try:
        return ("ok", fn(events, user, now=now))
    except Exception as e:
        # preserve exception type + message for equality checks
        return ("err", type(e), str(e))


def _results_equal(a, b):
    """Compare results, handling NaN values properly."""
    if a == b:
        return True
    # Special case: both are ("ok", nan)
    if (isinstance(a, tuple) and isinstance(b, tuple) and
        len(a) == 2 and len(b) == 2 and
        a[0] == "ok" and b[0] == "ok"):
        try:
            # Check if both results are NaN
            if math.isnan(a[1]) and math.isnan(b[1]):
                return True
        except (TypeError, ValueError):
            pass
    return False


def test_public_api_docstring_and_name_match():
    assert calc_before.__name__ == calc_after.__name__ == "calc_score"
    assert calc_before.__doc__ == calc_after.__doc__
    # quick signature sanity (avoid importing inspect to keep it simple)
    assert calc_before.__code__.co_argcount == calc_after.__code__.co_argcount


def test_equivalence_on_handpicked_adversarial_inputs():
    now = datetime(2025, 1, 1, 0, 0, 0)

    value_cases = [
        None, 0, 1, -1, 1.25, "3", "3.0", "nan", "inf", "", " 4 ",
        FloatBad(), StrNum(), StrBad(), object(),
    ]
    weight_cases = [None, 0, 1, -1, "2", "notint", 3.0, True, False, object()]

    user_cases = [
        {"tier": "free", "created_at": "2020-01-01"},
        {"tier": "pro", "created_at": "2010-12-31"},
        {"tier": "vip", "created_at": "not-a-date"},
        {"tier": None, "created_at": ""},
        {},
    ]

    for v in value_cases:
        for w in weight_cases:
            events = [{"value": v, "weight": w}]
            for user in user_cases:
                b = _call(calc_before, events, user, now)
                a = _call(calc_after, events, user, now)
                assert _results_equal(a, b), f"Results differ: {a} != {b}"


def test_equivalence_randomized_fuzz_deterministic():
    rng = random.Random(0)
    now = datetime(2025, 1, 1, 0, 0, 0)

    pool_values = [None, 0, 1, -2, 1.75, "5", "bad", "nan", FloatBad(), StrNum(), object()]
    pool_weights = [None, 0, 1, -3, "4", "x", 2.9, True, object()]
    pool_tiers = ["free", "pro", "vip", None, "other"]
    pool_dates = ["2020-01-01", "2019-02-29", "not-a-date", "", None]

    for _ in range(2000):
        n_events = rng.randint(0, 6)
        events = []
        for _ in range(n_events):
            e = {}
            # sometimes omit keys to exercise .get defaults
            if rng.random() < 0.85:
                e["value"] = rng.choice(pool_values)
            if rng.random() < 0.85:
                e["weight"] = rng.choice(pool_weights)
            events.append(e)

        user = {}
        if rng.random() < 0.9:
            user["tier"] = rng.choice(pool_tiers)
        if rng.random() < 0.9:
            user["created_at"] = rng.choice(pool_dates)

        b = _call(calc_before, events, user, now)
        a = _call(calc_after, events, user, now)
        assert _results_equal(a, b), f"Results differ: {a} != {b}"
