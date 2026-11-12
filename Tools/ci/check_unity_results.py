"""Fail CI on absent, empty, failed or skipped Unity Test Runner suites."""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def check_results(directory):
    errors = []
    for mode in ("editmode", "playmode"):
        path = Path(directory) / f"{mode}-results.xml"
        if not path.is_file():
            errors.append(f"{mode}: no Unity test report (tests not verified)")
            continue
        try:
            root = ET.parse(path).getroot()
            cases = list(root.iter("test-case"))
            passed = sum(case.get("result") == "Passed" for case in cases)
            if root.tag != "test-run" or root.get("result") != "Passed" or not cases or passed != len(cases):
                errors.append(f"{mode}: require nonempty, entirely passing test-run; {passed}/{len(cases)} test cases passed")
            else:
                print(f"{mode}: {passed} Unity test cases passed")
        except ET.ParseError:
            errors.append(f"{mode}: malformed Unity test report")
    for error in errors:
        print(f"::error::{error}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(check_results(sys.argv[1]))
