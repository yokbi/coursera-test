#!/usr/bin/env python3
"""Fail the build when Domain + Application line coverage drops below the threshold.

Merges every cobertura report produced by `dotnet test --collect:"XPlat Code Coverage"`,
taking the best hit count per line so unit and integration runs complement each other.
"""

import glob
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

THRESHOLD = 80.0
ASSEMBLIES = ("HabitTracker.Domain", "HabitTracker.Application")
REPORT_GLOB = "backend/tests/*/TestResults/*/coverage.cobertura.xml"


def main() -> int:
    reports = glob.glob(REPORT_GLOB)
    if not reports:
        print(f"No coverage reports matched {REPORT_GLOB}", file=sys.stderr)
        return 1

    hits: dict[tuple[str, str, str], int] = defaultdict(int)
    for path in reports:
        root = ET.parse(path).getroot()
        for package in root.iter("package"):
            assembly = package.get("name")
            if assembly not in ASSEMBLIES:
                continue
            for cls in package.iter("class"):
                for line in cls.iter("line"):
                    key = (assembly, cls.get("filename"), line.get("number"))
                    hits[key] = max(hits[key], int(line.get("hits")))

    if not hits:
        print(f"Reports contained no lines for {ASSEMBLIES}", file=sys.stderr)
        return 1

    per_assembly: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for (assembly, _, _), hit_count in hits.items():
        per_assembly[assembly][1] += 1
        if hit_count > 0:
            per_assembly[assembly][0] += 1

    covered = total = 0
    for assembly, (assembly_covered, assembly_total) in sorted(per_assembly.items()):
        covered += assembly_covered
        total += assembly_total
        print(f"{assembly}: {assembly_covered}/{assembly_total} lines = "
              f"{100 * assembly_covered / assembly_total:.1f}%")

    percentage = 100 * covered / total
    print(f"Combined Domain+Application: {covered}/{total} = {percentage:.1f}% "
          f"(threshold {THRESHOLD:.0f}%)")

    if percentage < THRESHOLD:
        print(f"::error::Coverage {percentage:.1f}% is below the {THRESHOLD:.0f}% threshold")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
