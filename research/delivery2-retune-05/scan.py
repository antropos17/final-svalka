"""Focused deterministic D2 price comparison; never writes the owning research."""
import argparse, copy, functools, hashlib, json, sys, time
from pathlib import Path
sys.dont_write_bytecode = True
p = argparse.ArgumentParser()
p.add_argument('--lab', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
p.add_argument('--prices', default='5,10,15,20,25,30,40,60')
a = p.parse_args()
started = time.perf_counter()
sys.path.insert(0, str(a.lab))
from lab import catalog, exact, plans
source_paths = [a.lab / 'lab' / (name + '.py') for name in ('catalog','model','plans','exact','reels')]
source_paths += [a.lab / 'data/two_era_committed.json']
hashes = {str(f.relative_to(a.lab)): hashlib.sha256(f.read_bytes()).hexdigest() for f in source_paths}
pin = json.loads((Path(__file__).parents[1] / 'skill-price-rebalance-05/results.json').read_text())
assert hashes == pin['source_hashes'], 'External model drift: stop before choosing a price'
c = catalog.load(str(source_paths[-1]))
for sid, price in pin['selected_prices'].items(): c.skills[sid]['price'] = price
frozen = copy.deepcopy(c.data)
tables = {}
@functools.lru_cache(maxsize=2048)
def advance(dist, price, key):
    out, mean = exact._sweep({(w,k):v for w,k,v in dist}, price, tables[key])
    return tuple((w,k,v) for (w,k),v in sorted(out.items())), mean
def evaluate(plan):
    dist = ((37,0,1.0),); total = 0.
    for _, price, rates in exact.phases(c, plan)[0]:
        key = (tuple(rates.symbol),rates.spin,rates.universal,rates.per_trash,rates.per_delivery,
               rates.loft_owned,rates.recycler_owned,rates.delivery_per,rates.delivery_amount)
        if key not in tables: tables[key] = exact._phase_table(c,rates)
        dist, mean = advance(dist,price,key); total += mean
    assert abs(sum(v for _,_,v in dist)-1) < 1e-10
    return total
contexts = []
for policy in ('workshop-first','building-first','family-order'):
    base = plans.generate(c,policy)
    parents = []
    for pos in range(len(base)+1):
        inserted = plans.insert_skill(c,base,'skill.delivery.1',pos)
        if inserted is not None: parents.append((evaluate(inserted),pos))
    for conditioning,pos in (('parent-earliest',parents[0][1]),('parent-best',min(parents)[1])):
        plan = plans.insert_skill(c,base,'skill.delivery.1',pos)
        contexts.append((policy,conditioning,pos,plan,evaluate(plan)))
result = {'base':'bff8b564bc55eeec24376a7cfa6c08631e4d27b0','method':'IEEE double phase DP over rational64 outcome probabilities; no MC',
          'source_hashes':hashes,'scope':'three fixed foreground policies, parent-earliest/best; no offline or all-orders claim','prices':{}}
for price in tuple(int(v) for v in a.prices.split(',')):
    c.skills['skill.delivery.2']['price'] = price
    cases = []
    for policy,label,parent_pos,plan,baseline in contexts:
        rows = []
        for pos in range(len(plan)):
            inserted = plans.insert_skill(c,plan,'skill.delivery.2',pos)
            if inserted is None: continue
            mean = evaluate(inserted)
            rows.append({'position':pos,'before_step':list(plan[pos]),'delta_spins':mean-baseline,'mean_spins':mean})
        best = min(rows,key=lambda r:r['delta_spins']); late = rows[-1]
        # Cross-check selected endpoints against the unchanged owning evaluator.
        for row in (best,late):
            ref = exact.evaluate(c,plans.insert_skill(c,plan,'skill.delivery.2',row['position']))['expected_total_spins']
            assert abs(ref-row['mean_spins']) < 1e-9
        cases.append({'policy':policy,'conditioning':label,'parent_position':parent_pos,'baseline_spins':baseline,
                      'beneficial':sum(r['delta_spins'] < -1e-9 for r in rows),'saving':sum(r['delta_spins'] > 1e-9 for r in rows),
                      'best':best,'late':late,'rows':rows})
    result['prices'][str(price)] = cases
    print(price,'worst best',max(r['best']['delta_spins'] for r in cases),'smallest late',min(r['late']['delta_spins'] for r in cases),flush=True)
restored = copy.deepcopy(c.data)
for skill in restored['skills']:
    if skill['id'] == 'skill.delivery.2': skill['price'] = 5
assert restored == frozen
assert hashes == {str(f.relative_to(a.lab)):hashlib.sha256(f.read_bytes()).hexdigest() for f in source_paths}
result['runtime_seconds'] = time.perf_counter()-started
a.output.parent.mkdir(parents=True,exist_ok=True)
a.output.write_text(json.dumps(result,indent=2)+'\n')
print('PASS runtime',result['runtime_seconds'])
