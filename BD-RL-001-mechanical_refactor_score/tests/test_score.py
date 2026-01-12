"""Unified test file for calc_score that works with both before and after implementations.

This test file uses PYTHONPATH to import from either repository_before or repository_after.
Run with:
    PYTHONPATH=/app/repository_before pytest tests/test_score.py
    PYTHONPATH=/app/repository_after pytest tests/test_score.py

Note: Some structural tests will FAIL for repository_before (expected) and PASS for repository_after.
"""
import os
import inspect
import pytest
from datetime import datetime
from pathlib import Path

# Import based on PYTHONPATH - will import from repository_before or repository_after
from app.score import calc_score

# Import the module to inspect its structure
import app.score as score_module


def test_matches_reference_vectors():
    """Ensure the implementation matches reference vectors."""
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
        result = calc_score(events, user, now)
        assert result == expected, f"Failed for events={events}, user={user}: expected {expected}, got {result}"


def test_empty_events():
    """Test with empty events list."""
    now = datetime(2025, 1, 1, 0, 0, 0)
    user = {"tier": "free", "created_at": "2020-01-01"}
    assert calc_score([], user, now) == 0.0


def test_none_values():
    """Test handling of None values."""
    now = datetime(2025, 1, 1, 0, 0, 0)
    events = [{"value": None, "weight": 1}]
    user = {"tier": "pro", "created_at": "2020-01-01"}
    result = calc_score(events, user, now)
    assert isinstance(result, float)


def test_invalid_weight():
    """Test handling of invalid weight values."""
    now = datetime(2025, 1, 1, 0, 0, 0)
    events = [{"value": "5", "weight": "invalid"}]
    user = {"tier": "free", "created_at": "2020-01-01"}
    result = calc_score(events, user, now)
    assert isinstance(result, float)


def test_negative_weight():
    """Test handling of negative weights."""
    now = datetime(2025, 1, 1, 0, 0, 0)
    events = [{"value": "10", "weight": -5}]
    user = {"tier": "pro", "created_at": "2020-01-01"}
    result = calc_score(events, user, now)
    assert result == 0.0  # Negative weights should become 0


def test_different_tiers():
    """Test different user tiers."""
    now = datetime(2025, 1, 1, 0, 0, 0)
    events = [{"value": "10", "weight": "1"}]
    
    free_user = {"tier": "free", "created_at": "2020-01-01"}
    pro_user = {"tier": "pro", "created_at": "2020-01-01"}
    vip_user = {"tier": "vip", "created_at": "2020-01-01"}
    
    free_score = calc_score(events, free_user, now)
    pro_score = calc_score(events, pro_user, now)
    vip_score = calc_score(events, vip_user, now)
    
    # VIP should have highest score due to loyalty bonus
    assert vip_score > pro_score
    assert pro_score > free_score


def test_invalid_date():
    """Test handling of invalid date strings."""
    now = datetime(2025, 1, 1, 0, 0, 0)
    events = [{"value": "10", "weight": "1"}]
    user = {"tier": "free", "created_at": "invalid-date"}
    result = calc_score(events, user, now)
    assert isinstance(result, float)


def test_multiple_events():
    """Test with multiple events."""
    now = datetime(2025, 1, 1, 0, 0, 0)
    events = [
        {"value": "1", "weight": "2"},
        {"value": "3", "weight": "4"},
    ]
    user = {"tier": "pro", "created_at": "2020-01-01"}
    result = calc_score(events, user, now)
    # Should accumulate: (1*2) + (3*4) = 2 + 12 = 14, plus loyalty bonus
    assert result > 14.0


# ============================================================================
# Structural tests - These will FAIL for repository_before and PASS for repository_after
# ============================================================================

def test_helper_functions_exist():
    """
    Structural requirement: Refactored code must have at least one helper function.
    This test will FAIL for repository_before (no helper functions) 
    and PASS for repository_after (has helper functions like _parse_float_lossy, _parse_int_default_one).
    """
    # Find all helper functions (functions starting with _)
    helpers = [
        name for name in dir(score_module)
        if name.startswith("_") and callable(getattr(score_module, name))
    ]
    
    assert len(helpers) >= 1, (
        f"Refactored code must have at least one helper function. "
        f"Found {len(helpers)} helper functions: {helpers}"
    )


def test_parsing_duplication_reduced():
    """
    Structural requirement: Duplicated parsing patterns must be reduced.
    This test will FAIL for repository_before (has duplicated float() and int() calls for value/weight parsing)
    and PASS for repository_after (uses helper functions, no duplication for value/weight parsing).
    
    Note: Other int()/float() calls (like years calculation) are not considered duplication.
    """
    pythonpath = os.environ.get("PYTHONPATH", "")
    
    # Get the source of calc_score function
    calc_score_source = inspect.getsource(calc_score)
    
    # Check which version we're testing
    is_after = "repository_after" in pythonpath or "repository_after" in str(inspect.getfile(score_module))
    
    if is_after:
        # After version: calc_score should use helper functions for value/weight parsing
        # Check that helper functions are called instead of direct parsing
        # The after code may use _parse_value or other helper function names
        has_helper_for_value = any(
            name.startswith("_") and name in calc_score_source
            for name in dir(score_module)
            if name.startswith("_") and callable(getattr(score_module, name))
        )
        
        assert has_helper_for_value, (
            f"calc_score function should use helper function(s) "
            f"instead of duplicating parsing logic."
        )
        
        # Count direct parsing calls in the event loop (the duplicated pattern area)
        # Look for float() calls in the value parsing section
        lines = calc_score_source.split('\n')
        in_event_loop = False
        value_parsing_float_calls = 0
        weight_parsing_int_calls = 0
        
        for line in lines:
            if 'for e in events:' in line or 'for e in events' in line:
                in_event_loop = True
            if in_event_loop and 'float(' in line and 'value' in line.lower():
                value_parsing_float_calls += 1
            if in_event_loop and 'int(' in line and 'weight' in line.lower():
                weight_parsing_int_calls += 1
            # Exit event loop when we hit the loyalty bonus section
            if 'loyalty bonus' in line.lower() or 'bonus =' in line:
                in_event_loop = False
        
        # After version should not have direct float()/int() calls for value/weight parsing
        assert value_parsing_float_calls == 0, (
            f"calc_score should not have direct float() calls for value parsing "
            f"(should use helper function). Found {value_parsing_float_calls} calls."
        )
        assert weight_parsing_int_calls == 0, (
            f"calc_score should not have direct int() calls for weight parsing "
            f"(should use helper function). Found {weight_parsing_int_calls} calls."
        )
    else:
        # Before version: has duplication - this test should FAIL
        # Count float() calls in the event loop (should be >= 2 for duplication)
        lines = calc_score_source.split('\n')
        in_event_loop = False
        float_calls_in_loop = 0
        
        for line in lines:
            if 'for e in events:' in line or 'for e in events' in line:
                in_event_loop = True
            if in_event_loop and 'float(' in line:
                float_calls_in_loop += 1
            if 'loyalty bonus' in line.lower():
                in_event_loop = False
        
        # Before version has duplicated float() calls - this assertion will fail
        assert float_calls_in_loop < 2, (
            f"Before version has duplicated float() parsing patterns in calc_score. "
            f"Found {float_calls_in_loop} float() calls in event loop. "
            f"This is expected to fail - the refactored version should reduce this duplication "
            f"by extracting parsing logic into helper functions."
        )


def test_code_uses_helper_functions():
    """
    Structural requirement: The refactored code must use helper functions to reduce duplication.
    This test will FAIL for repository_before (no helper function calls)
    and PASS for repository_after (calls helper functions like _parse_float_lossy).
    """
    calc_score_source = inspect.getsource(calc_score)
    
    # Check if helper functions are being called
    has_helper_calls = (
        "_parse_float_lossy" in calc_score_source or
        "_parse_int_default_one" in calc_score_source or
        any(name.startswith("_") and name in calc_score_source 
            for name in dir(score_module) 
            if name.startswith("_") and callable(getattr(score_module, name)))
    )
    
    assert has_helper_calls, (
        "Refactored code must use helper functions to reduce duplication. "
        "The calc_score function should call helper functions instead of duplicating parsing logic."
    )


def test_line_count_within_limit():
    """
    Structural requirement: Refactored code must not increase line count by more than +5 lines.
    This test will PASS for repository_after (meets requirement) and can be checked for before.
    """
    pythonpath = os.environ.get("PYTHONPATH", "")
    if "repository_before" in pythonpath:
        before_path = Path(__file__).parent.parent / "repository_before" / "app" / "score.py"
        after_path = Path(__file__).parent.parent / "repository_after" / "app" / "score.py"
    elif "repository_after" in pythonpath:
        before_path = Path(__file__).parent.parent / "repository_before" / "app" / "score.py"
        after_path = Path(__file__).parent.parent / "repository_after" / "app" / "score.py"
    else:
        pytest.skip("Could not determine which version is being tested")
    
    if not before_path.exists() or not after_path.exists():
        pytest.skip("Could not find before/after files for comparison")
    
    with open(before_path, "r", encoding="utf-8") as f:
        before_lines = len(f.readlines())
    
    with open(after_path, "r", encoding="utf-8") as f:
        after_lines = len(f.readlines())
    
    line_increase = after_lines - before_lines
    
    # Check which version we're testing
    is_after = "repository_after" in pythonpath or "repository_after" in str(inspect.getfile(score_module))
    
    if is_after:
        # After version: must meet the requirement of <= +5 lines
        assert line_increase <= 5, (
            f"Refactored code increased line count by {line_increase} lines, "
            f"which exceeds the maximum allowed increase of +5 lines. "
            f"Before: {before_lines} lines, After: {after_lines} lines."
        )
    else:
        # Before version: this test just documents the requirement
        # The actual check happens when testing the after version
        pass

