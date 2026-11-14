"""Check an explicitly authorized real-player ecology capture; does not assess visual quality."""
import json
import struct
import sys
from pathlib import Path

root = Path(sys.argv[1])
e = json.loads((root / 'ecology.json').read_text())
assert e['unity'] == '6000.6.9f1' and e['scene'] == 'WaterPrototype3D'
assert len(e['revision']) == 40 and e['capturedUtc']
assert e['modelDriven'] and not e['fixture']
assert e['lastTick'] - e['firstTick'] == 12000
assert len(e['frames']) == 8 and e['history']
for name in e['frames']:
    data = (root / name).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    width, height = struct.unpack('>II', data[16:24])
    assert width >= 1280 and height >= 720 and len(data) > 20000
print('Original ecology PNG sequences and real-player metadata present; inspect appearance and layout manually.')
