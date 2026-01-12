import sys
from pathlib import Path

# Ensure repository_before/ and repository_after/ are importable as top-level packages.
ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
