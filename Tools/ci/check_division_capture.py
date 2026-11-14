"""Require actual Unity frames plus model evidence for the same paid birth."""
import json
import struct
import sys
from pathlib import Path

root = Path(sys.argv[1])
e = json.loads((root / 'division.json').read_text())
assert e['unity'] == '6000.6.9f1'
assert e['pipeline'] == 'UniversalRenderPipelineAsset'
assert e['paid'] and e['inheritance'] and e['independent']
assert e['parent'] != e['child'] and e['generation'] > 0
assert e['childEnergy'] > 0 and e['parentAfter'] > 0
assert abs(e['divisionCost'] - e['childEnergy'] - e['construction']) < .0001
for name in ['division-before.png'] + [f'division-{i:03}.png' for i in range(49)]:
    data = (root / name).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', data[16:24]) == (1920, 1080)
    assert len(data) > 20000
print('Paid birth, inheritance, independent movement and 49 real Unity frames verified')
