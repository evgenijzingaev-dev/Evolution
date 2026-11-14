"""Check real Core CSV runs and exact independent repetition; not a Unity visual check."""
import csv
import math
import sys
from pathlib import Path

first, repeat = map(Path, sys.argv[1:3])
assert first.read_bytes() == repeat.read_bytes(), 'Seeded repeated CSV differs'
rows = list(csv.DictReader(first.open()))
groups = {}
for r in rows:
    groups.setdefault((r['scenario'], int(r['seed'])), []).append(r)
    assert all(math.isfinite(float(v)) for k, v in r.items() if k != 'scenario')
    assert int(r['alive']) == 48 + int(r['births']) - int(r['deaths'])
    assert int(r['alive']) <= 2048
assert set(groups) == {(s, n) for s in ('Sunlit', 'Dark', 'Hydrothermal', 'Natural') for n in (42, 7)}
for (scenario, seed), run in groups.items():
    assert len(run) == 61
    assert [float(r['seconds']) for r in run] == list(range(0, 601, 10))
    initial = float(run[0]['stored_energy'])
    for r in run:
        throughput = initial + sum(float(r[k]) for k in ('solar', 'external_chemical', 'external_organic'))
        assert abs(float(r['energy_residual'])) <= max(.001, throughput * .00005)
    last = run[-1]
    assert int(last['alive']) > 0 and int(last['generation']) >= 2
    field = {'Sunlit': 'photo_share', 'Dark': 'organic_share', 'Hydrothermal': 'hot_share'}.get(scenario)
    if field:
        assert float(last[field]) > .6
    else:
        assert float(last['photo_share']) > 0 and float(last['hot_share']) > 0
print('8 x 600s, 488 finite rows, paid population counts, energy ledger and byte-identical repeat PASS')
print('Relative matter/oxygen ledger bounds are additionally checked by NicheSelectionTests, not inferred from CSV.')
