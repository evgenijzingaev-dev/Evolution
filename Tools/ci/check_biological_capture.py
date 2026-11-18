"""Check opt-in evidence from the actual WaterPrototype3D URP player."""
import hashlib
import json
import math
import os
import struct
import sys
from pathlib import Path

ECOLOGY = [f'ecology-{x}.png' for x in (
    'light', 'temperature', 'nutrients', 'oxygen', 'chemical', 'strategies', 'optima', 'after-600s')]
FORMS = ('amoeboid', 'ciliate', 'spindle')
LIVING = ([f'living-{form}-{i:03}.png' for form in FORMS for i in range(40)]
          + ['division-before.png'] + [f'division-{i:03}.png' for i in range(49)]
          + [f'living-death-{i:03}.png' for i in range(81)])


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(root, mode, revision):
    root = Path(root)
    require(mode in ('ecology', 'living'), 'Unknown evidence mode')
    data = json.loads((root / ('ecology.json' if mode == 'ecology' else 'living-cells.json')).read_text())
    require(data['modelDriven'] is True and data['fixture'] is False, 'Fixture evidence is not accepted')
    require(data['unity'] == '6000.6.9f1' and data['scene'] == 'WaterPrototype3D', 'Wrong Unity/scene')
    require(bool(revision) and data['revision'] == revision, 'Wrong tested revision')
    render = data['render']
    require(render['passed'] is True and render['revision'] == revision, 'Unverified render')
    require(render['pipeline'] == 'UniversalRenderPipelineAsset' and render['graphicsApi'] != 'Null'
            and bool(render['graphicsDevice']), 'No actual URP device')
    expected = ECOLOGY if mode == 'ecology' else LIVING
    require([s['file'] for s in render['screenshots']] == expected, 'Missing or unordered real frames')
    hashes = []
    for shot in render['screenshots']:
        raw = (root / shot['file']).read_bytes()
        require(len(raw) > 20000 and raw[:8] == b'\x89PNG\r\n\x1a\n'
                and struct.unpack('>II', raw[16:24]) == (1920, 1080), 'Invalid PNG frame')
        require(shot['cameraFrames'] >= 2 and shot['tick'] >= 20, 'Game/camera did not advance')
        require(shot['luminanceRange'] > .12 and shot['luminanceDeviation'] > .005, 'Empty/flat frame')
        hashes.append(hashlib.sha256(raw).hexdigest())
    if mode == 'ecology':
        require(data['frames'] == ECOLOGY, 'Wrong ecology manifest')
        require(data['lastTick'] - data['firstTick'] == 12000, '600 simulated seconds were not observed')
        require(data['firstPopulation'] > 0 and data['lastPopulation'] > 0, 'No living ecosystem')
        require(data['births'] > 0 and data['deaths'] > 0, 'No observed population turnover')
        require(len(data['history']) >= 2, 'No generation history')
        for key in ('energyResidual', 'matterResidual', 'oxygenResidual'):
            require(math.isfinite(data[key]), 'Nonfinite resource ledger')
        require([s['surveyLayer'] for s in render['screenshots']] == [8, 6, 9, 10, 11, 2, 12, 2], 'Wrong map layers')
        require(len(set(hashes)) == len(hashes), 'Duplicate ecological views')
    else:
        require([m['form'] for m in data['movement']] == ['Amoeboid', 'Ciliate', 'Spindle'], 'Missing locomotion form')
        for m in data['movement']:
            require(m['frames'] == 40 and m['lastTick'] - m['firstTick'] == 40, 'Movement did not use real model ticks')
            require(sum((m['end'][k] - m['start'][k]) ** 2 for k in ('x', 'y', 'z')) > .0001,
                    'No real organism displacement')
        require(data['deathFrames'] == 81 and data['deathSequence'] > 0 and bool(data['deathIdentity'])
                and bool(data['deathCause']), 'No real model death')
        for offset, count in ((0, 40), (40, 40), (80, 40), (121, 49), (170, 81)):
            require(len(set(hashes[offset:offset + count])) >= 3, 'Static lifecycle sequence')
    return data


if __name__ == '__main__':
    evidence = validate(sys.argv[1], sys.argv[2], os.environ.get('EVOLUTION_PREVIEW_SHA'))
    print(f"Verified {sys.argv[2]}: {len(evidence['render']['screenshots'])} actual Unity frames, revision {evidence['revision']}")
