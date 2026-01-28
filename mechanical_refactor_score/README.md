

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

```bash
git diff --no-index repository_before repository_after > patches/task_001.patch
```
