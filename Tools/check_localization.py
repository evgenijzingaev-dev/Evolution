"""Reject inline text in the prototype's IMGUI; catalog coverage is tested by NUnit."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
errors = []
for path in (root / "Assets/Evolution/Runtime").rglob("*.cs"):
    source = path.read_text(encoding="utf-8")
    for call in re.finditer(r"GUILayout\.(?:Label|Button|Box|Toggle)\s*\(", source):
        # Current UI calls all obtain their visible text directly from the catalog.
        # Fail closed for new call sites so new text must follow the same convention.
        if not re.match(r"\s*GameText\.(?:Get|Format)\s*\(", source[call.end():]):
            line = source.count("\n", 0, call.start()) + 1
            errors.append(f"{path.relative_to(root)}:{line}: UI text must use GameText")
if errors:
    print("\n".join(errors), file=sys.stderr)
    sys.exit(1)
print("All current IMGUI labels and buttons use the localization catalog.")
