"""Smoke tests for AdventureBreaker — stdlib only, no network.

These exercise the pure logic (config, model parsing, the deterministic oracles,
coverage categorisation, the CLI parser, and the committed spine JSON) so CI has
real signal without touching a live backend.
"""
import json
import unittest
from pathlib import Path

from adventurebreaker import __version__, config, coverage, oracles
from adventurebreaker.client import ApiResult
from adventurebreaker.harness import build_parser
from adventurebreaker.models import GameResponse

REPO = Path(__file__).resolve().parent.parent


def _result(payload, *, status=200, method="POST", transport_error=None, raw=None):
    """Build an ApiResult from a JSON-able payload (or raw text)."""
    body = raw if raw is not None else json.dumps(payload) if payload is not None else ""
    return ApiResult(
        ok=(200 <= status < 300) and transport_error is None,
        status=status, latency_ms=1, url="http://test/ZorkOne", method=method,
        request_body=None, raw_text=body,
        json=payload if isinstance(payload, dict) else None,
        parse_error=None, transport_error=transport_error,
    )


class TestPackage(unittest.TestCase):
    def test_version(self):
        self.assertRegex(__version__, r"^\d+\.\d+\.\d+")


class TestConfig(unittest.TestCase):
    def test_resolve_both_games(self):
        for game in ("zork", "planetfall"):
            cfg = config.resolve(game, "prod")
            self.assertTrue(cfg["url"].startswith("https://"))
            self.assertGreater(cfg["max_score"], 0)

    def test_unknown_game_raises(self):
        with self.assertRaises(ValueError):
            config.resolve("nethack", "prod")

    def test_unknown_target_raises(self):
        with self.assertRaises(ValueError):
            config.resolve("zork", "staging")


class TestModels(unittest.TestCase):
    def test_envelope_parse_and_direction_mapping(self):
        g = GameResponse.from_json({
            "response": "West of House", "locationName": "West Of House",
            "score": 0, "moves": 1, "exits": [0, 2, 1],  # N, E, S as ints
            "inventory": ["leaflet"],
        })
        self.assertEqual(g.location_name, "West Of House")
        self.assertEqual(g.exits, ["N", "E", "S"])
        self.assertEqual(g.inventory, ["leaflet"])
        self.assertIn("West Of House", g.short())


class TestOraclesL0(unittest.TestCase):
    def test_clean_response_has_no_hits(self):
        res = _result({"response": "Taken.", "score": 10, "moves": 5, "inventory": ["sword"]})
        hits = oracles.run_all("take sword", res, None, max_score=350)
        self.assertEqual(hits, [])

    def test_server_error_is_critical(self):
        res = _result(None, status=500, transport_error="HTTP 500 Internal Server Error")
        hits = oracles.run_all("look", res, None, max_score=350)
        self.assertTrue(any(h.severity == "critical" for h in hits))

    def test_leaked_stack_trace(self):
        res = _result(None, status=200,
                      raw="Traceback (most recent call last): NullReferenceException")
        hits = oracles.run_all("xyzzy", res, None, max_score=350)
        self.assertIn("leaked_internals", {h.oracle for h in hits})

    def test_score_out_of_bounds(self):
        res = _result({"response": "You win.", "score": 9999, "moves": 3, "inventory": []})
        hits = oracles.run_all("look", res, None, max_score=350)
        self.assertIn("score_bounds", {h.oracle for h in hits})


class TestOraclesL1(unittest.TestCase):
    def test_taken_without_inventory_growth(self):
        prev = GameResponse.from_json({"locationName": "Kitchen", "inventory": [], "moves": 1})
        cur = {"response": "Taken.", "locationName": "Kitchen", "inventory": [], "moves": 2}
        res = _result(cur)
        hits = oracles.run_all("take sword", res, prev, max_score=350)
        self.assertIn("take_no_inv", {h.oracle for h in hits})

    def test_moves_regression(self):
        prev = GameResponse.from_json({"locationName": "Kitchen", "moves": 10})
        cur = {"response": "ok", "locationName": "Kitchen", "moves": 4}
        hits = oracles.run_all("wait", _result(cur), prev, max_score=350)
        self.assertIn("moves_regress", {h.oracle for h in hits})


class TestCoverage(unittest.TestCase):
    def test_categorize(self):
        self.assertEqual(coverage.categorize("take lantern"), "take-drop-scope")
        self.assertEqual(coverage.categorize("open mailbox"), "container")
        self.assertEqual(coverage.categorize("north"), "movement")
        self.assertEqual(coverage.categorize("take lantern. go north"), "multi-sentence")
        self.assertEqual(coverage.categorize("flibbertigibbet"), "other")

    def test_categories_are_known(self):
        self.assertIn("narrator-hallucination", coverage.CATEGORIES)


class TestCLI(unittest.TestCase):
    def test_parser_builds_and_parses(self):
        p = build_parser()
        args = p.parse_args(["new", "--game", "zork", "--target", "prod"])
        self.assertEqual(args.game, "zork")
        args = p.parse_args(["play", "examine", "the", "troll"])
        self.assertEqual(args.command, ["examine", "the", "troll"])


class TestSpines(unittest.TestCase):
    def test_spine_files_are_valid(self):
        for name in ("zork1.json", "planetfall.json"):
            data = json.loads((REPO / "adventurebreaker" / "spine" / name).read_text())
            self.assertIn("steps", data)
            self.assertGreater(len(data["steps"]), 0)
            step = data["steps"][0]
            for key in ("cmd", "expect", "http_replayable"):
                self.assertIn(key, step)


if __name__ == "__main__":
    unittest.main()
