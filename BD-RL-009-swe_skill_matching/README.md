# BD-RL-009-swe_skill_matching

This repository demonstrates a .NET console benchmark comparing two implementations of an employee skill matching system. The `repository_before` version uses loose dictionaries while `repository_after` introduces a typed catalog with a lightweight cache.

## Requirements

- [Docker](https://www.docker.com/get-started)
- [Docker Compose](https://docs.docker.com/compose/)

## Quick Start

### 1. Run Tests for `repository_before`

```bash
docker compose run --rm tests dotnet test tests/tests.csproj --filter RepositoryBeforeEMPerformanceTest -c Release --no-build -v minimal
```

### 2. Run Tests for `repository_after`

```bash
docker compose run --rm tests dotnet test tests/tests.csproj --filter RepositoryAfterEMPerformanceTest -c Release --no-build -v minimal
```

### 3. Run Evaluations

```bash
docker compose run --rm tests dotnet run --project evaluation/evaluation.csproj -- --iterations 5 --skills 160,320,640 --slots 20,40,80 --threshold 15 --output-dir evaluation
```

## Projects

| Directory | Description |
|-----------|-------------|
| `repository_before/` | Legacy implementation using in-memory dictionaries |
| `repository_after/` | Refined implementation with reusable catalog and typed records |
| `tests/` | xUnit test project validating both implementations |
| `evaluation/` | Performance evaluation harness |
