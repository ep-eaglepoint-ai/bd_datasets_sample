import pytest
from functools import lru_cache
from pathlib import Path

pytest_plugins = ("pytester",)

IMPL_DIR = Path(__file__).resolve().parent / "resources" / "division"


@lru_cache(maxsize=None)
def _load_divide_impl(filename: str) -> str:
    return (IMPL_DIR / filename).read_text()


@pytest.fixture
def division_suite_text() -> str:
    suite_path = Path(__file__).resolve().parents[1] / "repository_after" / "mathoperation" / "test_division.py"
    return suite_path.read_text()


def _run_division_suite(pytester, suite_text: str, impl: str):
    pytester.makepyfile(
        **{
            "mathoperation/__init__.py": "",
            "mathoperation/division.py": impl,
            "mathoperation/test_division.py": suite_text,
        }
    )
    return pytester.runpytest()


def _assert_min_failed(result, minimum: int = 1) -> None:
    outcomes = result.parseoutcomes()
    print("meta outcomes:", outcomes.get("failed", 0))  
    assert outcomes.get("failed", 0) >= minimum


def test_suite_detects_non_decimal_result(pytester, division_suite_text) -> None:
    result = _run_division_suite(
        pytester, division_suite_text, _load_divide_impl("broken_no_decimal.py")
    )
    _assert_min_failed(result)


def test_suite_detects_missing_zero_division_guard(pytester, division_suite_text) -> None:
    result = _run_division_suite(pytester, division_suite_text, _load_divide_impl("broken_zero_division.py"))
    _assert_min_failed(result)


def test_suite_detects_missing_value_error(pytester, division_suite_text) -> None:
    result = _run_division_suite(pytester, division_suite_text, _load_divide_impl("broken_invalid_input.py"))
    _assert_min_failed(result)


def test_suite_passes_for_correct_divide(pytester, division_suite_text) -> None:
    result = _run_division_suite(pytester, division_suite_text, _load_divide_impl("correct.py"))
    _assert_min_failed(result,0)
