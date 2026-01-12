from datetime import datetime
from repository_before.app.score import calc_score


def test_before_matches_reference_vectors():
    # Ensure the original behavior is locked in via fixed vectors.
    now = datetime(2025, 1, 1, 0, 0, 0)
    vectors = [
        ([], {"tier": "free", "created_at": "2020-01-01"}, 0.0),
        ([{"value": "1.5", "weight": "2"}], {"tier": "pro", "created_at": "2020-01-01"}, 3.22),
        ([{"value": None, "weight": None}, {"value": "3", "weight": -2}], {"tier": "vip", "created_at": "2010-01-01"}, 0.0),
        ([{"value": "2", "weight": "bad"}, {"value": "bad", "weight": "3"}], {"tier": "free", "created_at": "bad-date"}, 2.0),
        ([{"value": 5, "weight": 0}], {"tier": "pro", "created_at": "2024-12-31"}, 0.0),
        ([{"value": "-1", "weight": "-0"}], {"tier": "vip", "created_at": "2020-01-01"}, 0.0),
        ([{"value": 1.005, "weight": 1}], {"tier": "free", "created_at": "2020-01-01"}, 0.98),
        ([{"value": "100", "weight": "1"}], {"tier": "vip", "created_at": "2018-01-01"}, 116.4),
    ]
    for events, user, expected in vectors:
        assert calc_score(events, user, now) == expected
