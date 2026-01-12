import os


BEFORE_PATH = "repository_before/app/score.py"
AFTER_PATH = "repository_after/app/score.py"


def _read(path):
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


def test_helper_function_exists():
    """
    Mechanical refactor requirement:
    At least one helper function must exist in repository_after.
    """
    import repository_after.app.score as mod

    helpers = [
        name for name in dir(mod)
        if name.startswith("_") and callable(getattr(mod, name))
    ]

    assert len(helpers) >= 1


def test_parsing_duplication_reduced():
    """
    Structural-only test:
    Ensure repeated parsing patterns were de-duplicated in calc_score function.
    """
    before = _read(BEFORE_PATH)
    after = _read(AFTER_PATH)

    # Extract calc_score function from each file
    import re
    
    # Find calc_score function in before
    before_match = re.search(r'def calc_score\([^)]*\):.*?(?=\n\ndef |\nclass |\Z)', before, re.DOTALL)
    after_match = re.search(r'def calc_score\([^)]*\):.*?(?=\n\ndef |\nclass |\Z)', after, re.DOTALL)
    
    if not before_match or not after_match:
        # Fallback to whole file if we can't find the function
        before_calc_score = before
        after_calc_score = after
    else:
        before_calc_score = before_match.group(0)
        after_calc_score = after_match.group(0)
    
    # Count float() and int() calls in calc_score function only
    float_before = before_calc_score.count("float(")
    float_after = after_calc_score.count("float(")

    int_before = before_calc_score.count("int(")
    int_after = after_calc_score.count("int(")

    # After should have fewer float() calls in calc_score (moved to helper)
    # Note: int() might be the same if weight parsing wasn't extracted
    assert float_after < float_before, (
        f"calc_score function should have fewer float() calls after refactoring. "
        f"Before: {float_before}, After: {float_after}"
    )
    assert int_after <= int_before, (
        f"calc_score function should not have more int() calls after refactoring. "
        f"Before: {int_before}, After: {int_after}"
    )


def test_line_count_not_excessive():
    """
    Mechanical refactor guard:
    Refactor must not grow more than +5 lines.
    """
    before_lines = _read(BEFORE_PATH).splitlines()
    after_lines = _read(AFTER_PATH).splitlines()

    assert len(after_lines) <= len(before_lines) + 5
