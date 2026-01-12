# Mechanical Refactor: calc_score

This dataset task contains a production-style Python function with intentional quirks.
The objective is **pure structural de-duplication** while preserving **bit-for-bit** runtime behavior.

## Folder layout

- `repository_before/` original implementation
- `repository_after/` mechanically refactored implementation
- `tests/` equivalence + invariants tests
- `patches/` diff between before/after

## Run with Docker

### Build image
```bash
docker compose build
```

### Run tests (before – expected some failures)
```bash
docker compose run --rm -e PYTHONPATH=/app/repository_before app pytest -q
```

**Expected behavior:**
- Functional tests: ✅ PASS
- Structural tests (helper functions, duplication reduction): ❌ FAIL (expected - no improvements yet)

### Run tests (after – expected all pass)
```bash
docker compose run --rm -e PYTHONPATH=/app/repository_after app pytest -q
```

**Expected behavior:**
- Functional tests: ✅ PASS
- Structural tests (helper functions, duplication reduction): ✅ PASS (improvements present)

#### Run evaluation (compares both implementations)
```bash
docker compose run --rm app python evaluation/evaluation.py
```

This will:
- Run tests for both before and after implementations
- Run structure and equivalence tests
- Generate a report at `evaluation/YYYY-MM-DD/HH-MM-SS/report.json`

#### Run evaluation with custom output file
```bash
docker compose run --rm app python evaluation/evaluation.py --output /path/to/custom/report.json
```

## Run locally

### Install dependencies
```bash
pip install -r requirements.txt
```

### Run all tests
```bash
# Run all tests (quiet mode)
pytest -q

## Regenerate patch

From repo root:

```bash
git diff --no-index repository_before repository_after > patches/task_001.patch
```
