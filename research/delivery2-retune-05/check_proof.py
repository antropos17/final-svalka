"""Small saved-proof checks; no external model or Monte Carlo."""
import json
from pathlib import Path
root = Path(__file__).parent
d = json.loads((root/'results.json').read_text())
b = json.loads((root/'boundaries.json').read_text())
assert d['source_hashes'] == b['source_hashes']
assert len(d['prices']['20']) == 6
for old, new in zip(d['prices']['5'], d['prices']['20']):
    assert (old['policy'], old['conditioning']) == (new['policy'],new['conditioning'])
    assert old['saving'] == 0
    assert new['beneficial'] >= 1 and new['saving'] >= 1
    assert new['best']['position'] < new['late']['position']
    assert new['best']['before_step'][0] == new['late']['before_step'][0] == 'build'
    assert new['best']['delta_spins'] <= -0.44899
    assert new['late']['delta_spins'] >= 0.54449
print('PASS six reachable parent/policy cases: useful early, strictly worse purchase later, before chapter completion')
assert min(c['late']['delta_spins'] for c in b['prices']['7']) < 0
assert min(c['late']['delta_spins'] for c in b['prices']['8']) > 0
assert max(c['best']['delta_spins'] for c in b['prices']['29']) < 0
assert max(c['best']['delta_spins'] for c in d['prices']['30']) > 0
print('PASS tested neighboring sign boundaries7/8 and29/30; no untested all-integer/all-order claim')
assert max(c['best']['delta_spins'] for c in d['prices']['20']) > -1
assert min(c['late']['delta_spins'] for c in d['prices']['20']) < 1
print('PASS modest sub-one-spin worst-case margins disclosed; no manufactured strategic-depth claim')
