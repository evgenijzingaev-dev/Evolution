"""Validate opt-in real-player capture metadata and expected original PNG sequences."""
import json
import struct
import sys
from pathlib import Path

root = Path(sys.argv[1])
e = json.loads((root / 'living-cells.json').read_text())
assert e['unity'] == '6000.6.9f1' and e['scene'] == 'WaterPrototype3D'
assert e['modelDriven'] and not e['fixture']
assert len(e['revision']) == 40 and e['capturedUtc']
assert {m['form'] for m in e['movement']} == {'Amoeboid', 'Ciliate', 'Spindle'}
expected = []
for m in e['movement']:
    assert m['identity'] and m['frames'] == 40 and m['lastTick'] - m['firstTick'] == 40
    assert sum((m['end'][k] - m['start'][k]) ** 2 for k in ('x', 'y', 'z')) > .0001
    expected += [f'living-{m["form"].lower()}-{i:03}.png' for i in range(40)]
assert e['deathIdentity'] and e['deathSequence'] > 0 and e['deathFrames'] == 81
assert e['deathCause'] in ('Exhaustion', 'Damage')
expected += [f'living-death-{i:03}.png' for i in range(81)]
for name in expected:
    data = (root / name).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    assert struct.unpack('>II', data[16:24]) == (1920, 1080) and len(data) > 20000
print('Living capture metadata and 201 original PNGs present; visual motion still requires human review.')
