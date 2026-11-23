"""Independent arithmetic and saved-result checks; no external lab import."""
from fractions import Fraction
from itertools import product
import json
from pathlib import Path

outcomes = list(product(range(4), repeat=3))
base = sum(Fraction(23 if a == b == c else 8 if a == b or a == c or b == c else 3, 64)
           for a, b, c in outcomes)
trash = sum(Fraction(sum(symbol == 2 for symbol in outcome), 64) for outcome in outcomes)
deliveries = sum(Fraction((carry + sum(symbol == 3 for symbol in outcome)) // 3, 192)
                 for carry in range(3) for outcome in outcomes)
assert base == Fraction(113, 16)
assert trash == Fraction(3, 4)
assert deliveries == Fraction(1, 4)
assert sum(symbol == 2 for symbol in (0, 1, 3)) == 0
assert sum(symbol == 2 for symbol in (2, 2, 2)) == 3
assert (0 + 2) // 3 == 0 and (2 + 1) // 3 == 1
print("PASS independent64-outcome base113/16, trash3/4, stationary delivery1/4; qualifying and zero-delivery cases")

report = json.loads(Path(__file__).with_name("results.json").read_text(encoding="utf-8"))
assert report["selected_prices"] == {"slot.coin-boost": 120, "skill.universal.2": 160,
    "skill.recycling.1": 160, "skill.recycling.2": 90, "skill.delivery.1": 60, "skill.delivery.2": 5}
for skill in report["searches"]:
    for row in report["selected"][skill]:
        assert row["best"]["delta_spins"] <= -1
        assert row["margin_beneficial_count"] >= 1
        assert row["best"]["before_step"] != ["chapter-already-complete"]
    search = report["searches"][skill]
    assert search["trials"][str(search["selected_price"])]["qualifies"]
    assert not search["trials"][str(search["next_grid_price"])]["qualifies"]
print("PASS21 policy/parent-conditioning cases each have a pre-chapter >=1-spin window; next round prices miss margin")

for skill in ("slot.coin-boost", "skill.recycling.1", "skill.delivery.1",
              "skill.universal.2", "skill.recycling.2"):
    for row in report["selected"][skill]:
        late = [point for point in row["rows"] if point["before_step"][0] == "build"][-1]
        assert late["delta_spins"] > 0
print("PASS saving before final construction beats buying five node types; no post-completion purchase used for this claim")
assert Fraction(report["selected_prices"]["skill.universal.2"], 1) == 160
assert Fraction(report["selected_prices"]["skill.recycling.2"], 1) / trash == 120
assert Fraction(report["selected_prices"]["skill.delivery.1"], 1) / deliveries == 240
assert Fraction(report["selected_prices"]["skill.delivery.2"], 1) / deliveries == 20
print("PASS stationary payback: U2=160 spins, R2=120, D1=240, D2=20 (heuristics, not phase-DP substitutes)")
