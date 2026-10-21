#!/usr/bin/env python3
"""Close GitHub issues whose roadmap item is marked done in context/foundation/roadmap.md.

Matches issues by title prefix, e.g. roadmap ID "S-03" -> issue title starting with "[S-03]".
Only closes currently-open issues; never reopens, relabels, or edits anything else.
Read-only on the roadmap file - this script never writes back to roadmap.md.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

ROADMAP_PATH = Path("context/foundation/roadmap.md")
AT_A_GLANCE_HEADING = "## At a glance"
NEXT_HEADING_PREFIX = "## "


def parse_at_a_glance_table(text: str) -> list[tuple[str, str]]:
    """Return a list of (roadmap_id, status) pairs from the '## At a glance' table."""
    lines = text.splitlines()
    try:
        start = next(i for i, line in enumerate(lines) if line.strip() == AT_A_GLANCE_HEADING)
    except StopIteration:
        print(f"::warning::No '{AT_A_GLANCE_HEADING}' section found in {ROADMAP_PATH}")
        return []

    end = len(lines)
    for i in range(start + 1, len(lines)):
        if lines[i].startswith(NEXT_HEADING_PREFIX):
            end = i
            break

    rows: list[tuple[str, str]] = []
    for line in lines[start:end]:
        line = line.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) < 2:
            continue
        roadmap_id, status = cells[0], cells[-1]
        if roadmap_id in ("ID", "") or set(roadmap_id) <= {"-"}:
            continue  # header row or separator row
        if not re.fullmatch(r"[A-Za-z]+-\d+", roadmap_id):
            continue  # not a roadmap ID row
        rows.append((roadmap_id, status))
    return rows


def gh(*args: str) -> str:
    result = subprocess.run(["gh", *args], capture_output=True, text=True, check=True)
    return result.stdout


def main() -> int:
    repo = sys.argv[1] if len(sys.argv) > 1 else None
    if not repo:
        print("::error::usage: sync-roadmap-issues.py <owner/repo>")
        return 1

    if not ROADMAP_PATH.exists():
        print(f"i {ROADMAP_PATH} not found - nothing to sync.")
        return 0

    done_ids = {
        roadmap_id
        for roadmap_id, status in parse_at_a_glance_table(ROADMAP_PATH.read_text(encoding="utf-8"))
        if status.lower() == "done"
    }
    if not done_ids:
        print("i No roadmap items marked done - nothing to sync.")
        return 0

    raw = gh("issue", "list", "--repo", repo, "--state", "open", "--json", "number,title", "--limit", "200")
    open_issues = json.loads(raw)

    closed_any = False
    for roadmap_id in sorted(done_ids):
        prefix = f"[{roadmap_id}]"
        matches = [issue for issue in open_issues if issue["title"].startswith(prefix)]
        if not matches:
            continue
        for issue in matches:
            number = issue["number"]
            comment = (
                f"Auto-closed: roadmap item `{roadmap_id}` is marked `done` in "
                f"`{ROADMAP_PATH.as_posix()}` as of this push."
            )
            print(f"Closing #{number} ({issue['title']}) - {roadmap_id} is done")
            gh("issue", "close", str(number), "--repo", repo, "--comment", comment)
            closed_any = True

    if not closed_any:
        print("i All roadmap items marked done already have their issues closed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
