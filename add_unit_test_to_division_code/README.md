# Division Meta Test Suite

This project demonstrates **meta testing** — a technique where we write tests that verify our test suite itself works correctly.

## Quick Start

```bash
docker compose run --rm tests
```

## What Are Meta Tests?

When you write unit tests for a function, how do you know your tests are actually good? A bad test might pass even when the code is broken.

**Meta tests solve this problem.** They run your test suite against intentionally broken implementations to verify that your tests catch the bugs.

## How It Works

### The Setup

1. **Correct Implementation** (`repository_after/mathoperation/division.py`)  
   The working `divide()` function that handles edge cases properly.

2. **Test Suite** (`repository_after/mathoperation/test_division.py`)  
   Unit tests that verify the `divide()` function works correctly.

3. **Broken Implementations** (`tests/resources/division/`)  
   Intentionally buggy versions of `divide()` used to test the test suite:
   - `broken_no_decimal.py` — Returns plain float instead of Decimal
   - `broken_zero_division.py` — Doesn't raise error when dividing by zero
   - `broken_invalid_input.py` — Doesn't validate non-numeric inputs
   - `correct.py` — The correct implementation (control case)

4. **Meta Tests** (`tests/test_division_meta.py`)  
   Tests that run the test suite against each broken implementation.

### What Happens When You Run Tests

The meta tests check that your test suite **detects bugs** in broken implementations:

| Meta Test | Tests Against | Expected Inner Result | Meta Test Passes If |
|-----------|---------------|----------------------|---------------------|
| `test_suite_detects_non_decimal_result` | `broken_no_decimal.py` | At least 1 failure | Test suite catches the bug |
| `test_suite_detects_missing_zero_division_guard` | `broken_zero_division.py` | At least 1 failure | Test suite catches the bug |
| `test_suite_detects_missing_value_error` | `broken_invalid_input.py` | At least 1 failure | Test suite catches the bug |
| `test_suite_passes_for_correct_divide` | `correct.py` | All pass | Test suite accepts correct code |

### Understanding the Output

When you run the tests, you'll see output like this:

```
tests/test_division_meta.py::test_suite_detects_non_decimal_result 
...
mathoperation/test_division.py .F.    ← Inner test run (1 failure)
FAILED mathoperation/test_division.py::...
meta outcomes: 1                       ← Meta test sees 1 failure
PASSED                                 ← Meta test PASSES (bug was detected!)
```

**Don't be alarmed by the "FAILED" messages!** These are the *inner* test runs against broken code. The failures prove your test suite is working correctly. The final `PASSED` for each meta test confirms your test suite catches that particular bug.

### Final Result

```
======= 4 passed in 0.07s =======
```

All 4 meta tests passing means your test suite:
- ✅ Catches when division doesn't return Decimal
- ✅ Catches missing zero-division error handling
- ✅ Catches missing input validation
- ✅ Passes correct implementations

## Project Structure

```
├── repository_after/
│   └── mathoperation/
│       ├── division.py          # The function being tested
│       └── test_division.py     # Unit tests for divide()
├── tests/
│   ├── resources/division/      # Broken implementations
│   │   ├── broken_no_decimal.py
│   │   ├── broken_zero_division.py
│   │   ├── broken_invalid_input.py
│   │   └── correct.py
│   └── test_division_meta.py    # Meta tests
├── docker-compose.yml
└── Dockerfile
```

## Why Meta Testing?

Meta testing ensures your tests have **meaningful coverage**. Without it, you might have tests that:
- Always pass regardless of bugs
- Miss important edge cases
- Give false confidence in code quality

By proving your tests fail on broken code, you prove they're actually protecting you.
