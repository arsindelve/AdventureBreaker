"""Persistent, cross-run coverage ledger.

The container AdventureBreaker runs in is ephemeral and `runs/` is gitignored,
so every session would otherwise start blind and re-walk old ground. This module
persists coverage **inside the repo** (committed, survives container death) and
computes the untested "frontier" so each run targets new avenues.

Files (all under `coverage/`, committed — NOT gitignored):

    coverage/journal.jsonl     append-only event log; one line per probe
    coverage/state.json        rolled-up (game, area, category) status (generated)
    coverage/MAP.md            human-readable coverage map + frontier (generated)
    coverage/areas.json        the universe of areas per game (locations + mechanics)
    coverage/findings.jsonl    durable findings (survive the ephemeral runs/ dir)
    coverage/FINDINGS.md       human-readable findings (generated)
    coverage/known_issues.json snapshot of open GitHub issues (so we never re-file)

Why append-only JSONL: parallel sessions never conflict, the full history is
replayable, and roll-up is a pure function of the journal.

Staleness (the prod-lags-`main` lesson): every event records the target code
revision (`AB_TARGET_SHA`). The frontier re-opens a cell once the code revision
advances past the one a "clean" result was recorded against, so a fix or new
code automatically resurfaces for re-test and a clean is never banked forever.
"""
from __future__ import annotations

import json
import os
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

# Coverage lives at the repo root by default (one level up from this package),
# so it is committed alongside the code rather than in the gitignored runs/ dir.
COVERAGE_DIR = Path(os.environ.get(
    "AB_COVERAGE_DIR", str(Path(__file__).resolve().parent.parent / "coverage")))

# The probe taxonomy. Auto-cover infers a coarse category from the command; the
# driver can record any of these precisely with `cover --category`.
CATEGORIES = [
    "movement", "examine-scenery", "container", "take-drop-scope", "light-dark",
    "parser-pronoun", "get-post-parity", "multi-sentence", "save-restore",
    "death", "npc-conversation", "narrator-hallucination",
    "narrator-spoiler-injection", "character-break", "puzzle-step", "other",
]

# Severity ordering for "best" (worst-seen) aggregation.
_SEV_ORDER = ["clean", "info", "low", "medium", "high", "critical"]


def _now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def _target_sha() -> str:
    """Identifier for the code revision under test (ZorkAI origin/main short SHA).

    The driver sets AB_TARGET_SHA from the ZorkAI checkout; falls back to a
    sentinel so events are still recorded (just without staleness tracking).
    """
    return os.environ.get("AB_TARGET_SHA", "unknown")


def _worse(a: str, b: str) -> str:
    ia = _SEV_ORDER.index(a) if a in _SEV_ORDER else 0
    ib = _SEV_ORDER.index(b) if b in _SEV_ORDER else 0
    return a if ia >= ib else b


# -- command -> category heuristic (for passive auto-cover) ------------------
_VERB_CATEGORY = [
    (("look in", "look inside", "search", "open", "close", "put "), "container"),
    (("examine", "look at", "read", "x ", "look under", "search"), "examine-scenery"),
    (("take", "get ", "drop", "pick up", "grab"), "take-drop-scope"),
    (("turn on", "turn off", "light", "extinguish"), "light-dark"),
    (("save", "restore"), "save-restore"),
    (("talk", "say", "ask", "tell", "floyd,", "hello"), "npc-conversation"),
    (("n", "s", "e", "w", "ne", "nw", "se", "sw", "up", "down", "in", "out",
      "north", "south", "east", "west", "enter", "exit", "port", "starboard",
      "go "), "movement"),
]


def categorize(command: str) -> str:
    """Best-effort coarse category for a raw command (auto-cover only)."""
    c = (command or "").strip().lower()
    if "." in c:                       # period-chained commands
        return "multi-sentence"
    for prefixes, cat in _VERB_CATEGORY:
        for p in prefixes:
            if c == p.strip() or c.startswith(p):
                return cat
    return "other"


# -- low-level IO ------------------------------------------------------------
def _ensure_dir() -> None:
    COVERAGE_DIR.mkdir(parents=True, exist_ok=True)


def log_event(*, game: str, target: str, run_id: str, area: Optional[str],
              command: str, result: str, severity: str = "clean",
              category: Optional[str] = None, finding_id: Optional[str] = None,
              session_id: Optional[str] = None) -> None:
    """Append one coverage event. Never raises into the caller's hot path."""
    try:
        _ensure_dir()
        event = {
            "ts": _now(), "run_id": run_id, "session_id": session_id,
            "game": game, "target": target, "target_sha": _target_sha(),
            "area": area or "?", "category": category or categorize(command),
            "command": command, "result": result, "severity": severity,
            "finding_id": finding_id,
        }
        with (COVERAGE_DIR / "journal.jsonl").open("a", encoding="utf-8") as f:
            f.write(json.dumps(event) + "\n")
    except Exception:
        pass  # coverage must never break a probe


def load_journal() -> List[Dict[str, Any]]:
    p = COVERAGE_DIR / "journal.jsonl"
    if not p.exists():
        return []
    out = []
    for line in p.read_text(encoding="utf-8").splitlines():
        if line.strip():
            try:
                out.append(json.loads(line))
            except Exception:
                pass
    return out


def load_areas() -> Dict[str, List[str]]:
    p = COVERAGE_DIR / "areas.json"
    if not p.exists():
        return {}
    return json.loads(p.read_text(encoding="utf-8"))


# -- roll-up: journal -> per-cell state -------------------------------------
def _cell_key(e: Dict[str, Any]) -> Tuple[str, str, str]:
    return (e.get("game", "?"), e.get("area", "?"), e.get("category", "?"))


def rollup() -> Dict[str, Any]:
    """Fold the journal into per-cell state and regenerate state.json + MAP.md."""
    cells: Dict[str, Dict[str, Any]] = {}
    for e in load_journal():
        g, area, cat = _cell_key(e)
        key = f"{g}|{area}|{cat}"
        c = cells.setdefault(key, {
            "game": g, "area": area, "category": cat, "count": 0,
            "best_severity": "clean", "last_ts": None, "last_target_sha": None,
            "last_result": None, "finding_ids": [],
        })
        c["count"] += 1
        c["best_severity"] = _worse(c["best_severity"], e.get("severity", "clean"))
        c["last_ts"] = e.get("ts")
        c["last_target_sha"] = e.get("target_sha")
        c["last_result"] = e.get("result")
        fid = e.get("finding_id")
        if fid and fid not in c["finding_ids"]:
            c["finding_ids"].append(fid)
    state = {"generated": _now(), "target_sha": _target_sha(), "cells": cells}
    _ensure_dir()
    (COVERAGE_DIR / "state.json").write_text(json.dumps(state, indent=2))
    _render_map(state)
    return state


def _render_map(state: Dict[str, Any]) -> None:
    areas = load_areas()
    cells = state["cells"]
    lines = [
        "# AdventureBreaker coverage map",
        "",
        f"_Generated {state['generated']} · target_sha `{state['target_sha']}`_",
        "",
        "Per-area category coverage (covered / total categories), worst severity "
        "seen, and the revision last tested against.",
        "",
    ]
    for game in sorted(areas):
        game_areas = areas[game]
        lines.append(f"## {game}")
        lines.append("")
        lines.append("| Area | Covered | Worst | Last sha | Findings |")
        lines.append("|---|---|---|---|---|")
        for area in game_areas:
            covered = [c for c in cells.values()
                       if c["game"] == game and c["area"] == area]
            cats = {c["category"] for c in covered}
            worst = "clean"
            fids: List[str] = []
            sha = "-"
            for c in covered:
                worst = _worse(worst, c["best_severity"])
                fids += c["finding_ids"]
                sha = c["last_target_sha"] or sha
            mark = "" if cats else "  ⬅ untouched"
            lines.append(f"| {area} | {len(cats)}/{len(CATEGORIES)} | {worst} | "
                         f"`{sha}` | {', '.join(sorted(set(fids))) or '-'} |{mark}")
        lines.append("")
    (COVERAGE_DIR / "MAP.md").write_text("\n".join(lines), encoding="utf-8")


# -- frontier: what to test next --------------------------------------------
def frontier(game: str, top: int = 25) -> List[Dict[str, Any]]:
    """Rank the least-covered cells for `game`. Lower priority value = test sooner.

    0 = never tested
    1 = tested clean but the code revision has advanced since (stale -> re-test)
    Cells with an open finding or a current clean result are not surfaced.
    """
    areas = load_areas().get(game, [])
    state = rollup()
    cells = state["cells"]
    cur_sha = state["target_sha"]
    known = _open_issue_areas()

    rows: List[Dict[str, Any]] = []
    for area in areas:
        for cat in CATEGORIES:
            key = f"{game}|{area}|{cat}"
            c = cells.get(key)
            if c is None:
                rows.append({"priority": 0, "area": area, "category": cat,
                             "reason": "never tested",
                             "known_issue": area in known})
            elif c["finding_ids"]:
                continue  # already produced a finding here
            elif (cur_sha != "unknown" and c["last_target_sha"] not in (None, cur_sha)):
                rows.append({"priority": 1, "area": area, "category": cat,
                             "reason": f"stale (tested @ {c['last_target_sha']}, "
                                       f"now {cur_sha})",
                             "known_issue": area in known})
            # else: clean & current -> covered, skip
    rows.sort(key=lambda r: (r["priority"], r["known_issue"], r["area"], r["category"]))
    return rows[:top]


def _open_issue_areas() -> set:
    p = COVERAGE_DIR / "known_issues.json"
    if not p.exists():
        return set()
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
    except Exception:
        return set()
    areas = set()
    for it in data.get("issues", []):
        for a in it.get("areas", []):
            areas.add(a)
    return areas


# -- durable findings (survive the ephemeral runs/ dir) ---------------------
def record_finding(finding: Dict[str, Any]) -> str:
    _ensure_dir()
    finding = dict(finding)
    finding.setdefault("ts", _now())
    finding.setdefault("target_sha", _target_sha())
    finding.setdefault("status", "open")
    fid = finding.get("id") or _next_finding_id()
    finding["id"] = fid
    with (COVERAGE_DIR / "findings.jsonl").open("a", encoding="utf-8") as f:
        f.write(json.dumps(finding) + "\n")
    _render_findings()
    return fid


def _load_findings() -> List[Dict[str, Any]]:
    p = COVERAGE_DIR / "findings.jsonl"
    if not p.exists():
        return []
    return [json.loads(l) for l in p.read_text(encoding="utf-8").splitlines() if l.strip()]


def _next_finding_id() -> str:
    return f"AB-{len(_load_findings()) + 1:03d}"


def _render_findings() -> None:
    findings = _load_findings()
    order = {s: i for i, s in enumerate(reversed(_SEV_ORDER))}
    findings.sort(key=lambda f: order.get(f.get("severity", "low"), 99))
    lines = ["# AdventureBreaker durable findings", "",
             f"_Generated {_now()} · {len(findings)} finding(s)_", ""]
    for f in findings:
        status = f.get("status", "open")
        lines.append(f"## {f.get('id')} [{f.get('severity','?').upper()}] "
                     f"{f.get('title','(untitled)')}  · _{status}_")
        lines.append("")
        lines.append(f"- game `{f.get('game','?')}` · area `{f.get('area','?')}` · "
                     f"category `{f.get('category','?')}` · target_sha "
                     f"`{f.get('target_sha','?')}`")
        if f.get("command"):
            lines.append(f"- command: `{f.get('command')}`")
        if f.get("detail"):
            lines.append(""); lines.append(f.get("detail"))
        lines.append("")
    (COVERAGE_DIR / "FINDINGS.md").write_text("\n".join(lines), encoding="utf-8")


def import_issues(issues: List[Dict[str, Any]]) -> int:
    """Persist a snapshot of open GitHub issues so the frontier de-prioritizes
    already-known areas and the driver never re-files. `issues` items look like
    {number, title, areas:[...]}."""
    _ensure_dir()
    (COVERAGE_DIR / "known_issues.json").write_text(
        json.dumps({"generated": _now(), "issues": issues}, indent=2))
    return len(issues)
