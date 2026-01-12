# Employee Matcher Performance Report

**Run ID:** `ef93f4cd`  
**Started:** 2025-12-19T15:17:37.1382771Z  
**Finished:** 2025-12-19T15:17:45.5456166Z  
**Duration:** 8.41 seconds

---

## Environment

| Property | Value |
|----------|-------|
| .NET | 8.0.19 |
| OS | Microsoft Windows 10.0.26100 |
| OS Arch | X64 |
| Process Arch | X64 |
| Git Commit | `1a41d13` |
| Git Branch | `main` |

---

## Parameters

| Parameter | Value |
|-----------|-------|
| Iterations | 5 |
| Threshold | 15 |
| Scenarios | skills160_slots20, skills320_slots40, skills640_slots80 |

---

## Seed Statistics

| Entity | Count |
|--------|-------|
| Employees | 1000 |
| Skills | 4473 |
| Availability Slots | 3473 |

---

## Summary Results

| Metric | Before (Naive) | After (Optimized) | Improvement |
|--------|----------------|-------------------|-------------|
| Average Response | 90.93 ms | 16.87 ms | **5.39x faster** |
| Improvement | - | - | 81.5% |

---

## Detailed Results by Scenario

| Scenario | Skills | Time Slots | Before (ms) | After (ms) | Speedup |
|----------|--------|------------|-------------|------------|---------|
| skills160_slots20 | 160 | 20 | 97.22 | 18.85 | 5.16x |
| skills320_slots40 | 320 | 40 | 92.11 | 18.31 | 5.03x |
| skills640_slots80 | 640 | 80 | 83.47 | 13.44 | 6.21x |

---

## Conclusion

✅ **Excellent optimization!** The optimized implementation is **5.39x faster** than the naive version.
