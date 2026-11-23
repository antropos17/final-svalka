"""Bounded, deterministic price-only search using a read-only external lab.

No simulation, production writes, bytecode, or copied economy model. Final means use
the lab's float phase sweep over rational probabilities, not rational arithmetic.
"""
import argparse
import copy
import functools
import hashlib
import json
from pathlib import Path
import sys
import time

sys.dont_write_bytecode = True
POLICIES = ("workshop-first", "building-first", "family-order")
PROBLEM = ("skill.delivery.1", "skill.universal.2", "skill.recycling.2", "skill.delivery.2")
GRID = 10
MARGIN = 1.0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--lab", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    started = time.perf_counter()
    sys.path.insert(0, str(args.lab.resolve()))
    from lab import catalog, exact, plans
    sources = [args.lab / "lab" / (name + ".py") for name in ("catalog", "model", "plans", "exact", "reels")]
    sources.append(args.lab / "data" / "two_era_committed.json")
    hashes = {str(p.relative_to(args.lab)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
    original = catalog.load(str(sources[-1]))
    current = catalog.Catalog(copy.deepcopy(original.data))
    original_prices = {sid: s["price"] for sid, s in original.skills.items()}
    phase_tables = {}
    evaluations = 0

    @functools.lru_cache(maxsize=4096)
    def advance(distribution, price, key):
        before = {(w, c): mass for w, c, mass in distribution}
        after, spins = exact._sweep(before, price, phase_tables[key])
        return tuple((w, c, mass) for (w, c), mass in sorted(after.items())), spins

    def evaluate(c, plan):
        nonlocal evaluations
        evaluations += 1
        # Memoization only: uses the owning lab's phase construction and sweep unchanged.
        dist = ((c.rules["initial_coins"], 0, 1.0),)
        total = 0.0
        for _, price, rates in exact.phases(c, plan)[0]:
            key = (tuple(rates.symbol), rates.spin, rates.universal, rates.per_trash, rates.per_delivery,
                   rates.loft_owned, rates.recycler_owned, rates.delivery_per, rates.delivery_amount)
            if key not in phase_tables:
                phase_tables[key] = exact._phase_table(c, rates)
            dist, spins = advance(dist, price, key)
            total += spins
        assert abs(sum(row[2] for row in dist) - 1.0) < 1e-10
        return total

    def scan(c, plan, sid):
        baseline = evaluate(c, plan)
        rows = []
        for position in range(len(plan) + 1):
            inserted = plans.insert_skill(c, plan, sid, position)
            if inserted is None:
                continue
            mean = evaluate(c, inserted)
            rows.append({"position": position, "mean_spins": mean, "delta_spins": mean - baseline,
                         "before_step": list(plan[position]) if position < len(plan) else ["chapter-already-complete"]})
        assert rows
        return {"skill": sid, "price": c.skills[sid]["price"], "baseline_spins": baseline,
                "legal_count": len(rows), "strict_beneficial_count": sum(r["delta_spins"] < -1e-9 for r in rows),
                "margin_beneficial_count": sum(r["delta_spins"] <= -MARGIN for r in rows),
                "best": min(rows, key=lambda r: r["delta_spins"]),
                "latest": rows[-1], "rows": rows}

    base_plans = {policy: plans.generate(original, policy) for policy in POLICIES}
    for policy, plan in base_plans.items():
        owning_mean = exact.evaluate(original, plan)["expected_total_spins"]
        assert abs(evaluate(original, plan) - owning_mean) < 1e-10
        print("baseline", policy, owning_mean, flush=True)

    def contexts(c, sid):
        for policy, base in base_plans.items():
            parent = c.skills[sid].get("requires_skill")
            if parent is None:
                yield policy, "no-parent", base, None
                continue
            parent_scan = scan(c, base, parent)
            for label, position in (("parent-earliest", parent_scan["rows"][0]["position"]),
                                    ("parent-best", parent_scan["best"]["position"])):
                plan = plans.insert_skill(c, base, parent, position)
                assert plan is not None
                yield policy, label, plan, {"skill": parent, "position": position,
                                          "parent_delta_spins": evaluate(c, plan) - evaluate(c, base)}

    def assess(c, sid):
        results = []
        for policy, conditioning, plan, parent in contexts(c, sid):
            result = scan(c, plan, sid)
            result.update(policy=policy, conditioning=conditioning, parent=parent)
            results.append(result)
        return results

    old = {sid: assess(original, sid) for sid in original.skills}
    print("old prices assessed", round(time.perf_counter() - started, 3), flush=True)
    searches = {}
    for sid in PROBLEM:
        trials = {}
        # The first10-coin-grid run showed delivery2 at10 misses the margin by0.05885.
        # Use a5-coin grid only for that short final-era horizon, preserving the margin.
        grid = 5 if sid == "skill.delivery.2" else GRID

        def trial(units):
            price = units * grid
            if price not in trials:
                current.skills[sid]["price"] = price
                rows = assess(current, sid)
                worst_best = max(row["best"]["delta_spins"] for row in rows)
                trials[price] = {"worst_best_delta_spins": worst_best,
                                 "qualifies": worst_best <= -MARGIN}
                print("trial", sid, price, round(worst_best, 6), "seconds", round(time.perf_counter() - started, 3), flush=True)
            return trials[price]["qualifies"]

        lower, upper = 1, original_prices[sid] // grid
        assert trial(lower), "No positive round price meets the chosen one-spin margin"
        while lower < upper:
            middle = (lower + upper + 1) // 2
            if trial(middle):
                lower = middle
            else:
                upper = middle - 1
        selected = lower * grid
        assert trial(lower)
        if selected < original_prices[sid]:
            assert not trial(lower + 1)
        current.skills[sid]["price"] = selected
        searches[sid] = {"grid_coins": grid, "selected_price": selected, "next_grid_price": selected + grid,
                         "trials": {str(k): v for k, v in sorted(trials.items())}}
        print("SELECTED", sid, selected, flush=True)

    new = {sid: assess(current, sid) for sid in current.skills}
    for sid in PROBLEM:
        assert all(row["margin_beneficial_count"] >= 1 for row in new[sid])
        assert all(row["best"]["position"] < len(base_plans[row["policy"]]) + (1 if row["parent"] else 0) for row in new[sid])
    for sid in ("slot.coin-boost", "skill.recycling.1"):
        assert current.skills[sid]["price"] == original_prices[sid]
        assert all(row["strict_beneficial_count"] > 0 for row in new[sid])
    assert all(any(row["delta_spins"] > 0 for row in report["rows"]) for reports in new.values() for report in reports)
    # The entire catalog outside four price fields must remain identical.
    restored = copy.deepcopy(current.data)
    for skill in restored["skills"]:
        skill["price"] = original_prices[skill["id"]]
    assert restored == original.data
    # Independent oracle check: one selected beneficial plan per problematic node and policy.
    for sid in PROBLEM:
        for report, (_, _, plan, _) in zip(new[sid], contexts(current, sid)):
            inserted = plans.insert_skill(current, plan, sid, report["best"]["position"])
            reference = exact.evaluate(current, inserted)["expected_total_spins"]
            assert abs(reference - report["best"]["mean_spins"]) < 1e-9
    assert hashes == {str(p.relative_to(args.lab)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
    result = {"format": "jtn-skill-price-search/1", "source_base": "042fb52/r8", "external_lab": str(args.lab),
              "source_hashes": hashes, "method": "lab float phase DP over rational64 outcomes; memoization only; no MC",
              "policies": list(POLICIES), "grid_coins": GRID, "required_margin_spins": MARGIN,
              "scope": "Foreground only, no offline credit; three fixed policies; no claim for all legal orders or 120 samples",
              "old_prices": original_prices, "selected_prices": {sid: s["price"] for sid, s in current.skills.items()},
              "searches": searches, "old": old, "selected": new,
              "evaluation_count": evaluations, "phase_cache": advance.cache_info()._asdict(),
              "runtime_seconds": time.perf_counter() - started, "failures": []}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("PASS", result["selected_prices"], "seconds", result["runtime_seconds"], flush=True)


if __name__ == "__main__":
    main()
