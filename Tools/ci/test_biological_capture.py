"""Negative tests of evidence validation using in-memory metadata, never game screenshots."""
import copy
import json
import struct
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from check_biological_capture import validate, ECOLOGY, LIVING


class EvidenceChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def sample(self, mode):
        names = ECOLOGY if mode == 'ecology' else LIVING
        report = dict(modelDriven=True, fixture=False, unity='6000.6.9f1', scene='WaterPrototype3D', revision='test-sha',
                      render=dict(passed=True, revision='test-sha', pipeline='UniversalRenderPipelineAsset',
                                  graphicsApi='OpenGLCore', graphicsDevice='validator-test', screenshots=[
                                      dict(file=name, cameraFrames=4, tick=20 + i, luminanceRange=.8, luminanceDeviation=.2,
                                           surveyLayer=([8, 6, 9, 10, 11, 2, 12, 2][i] if mode == 'ecology' else 0))
                                      for i, name in enumerate(names)]))
        if mode == 'ecology':
            report.update(frames=names, firstTick=20, lastTick=12020, firstPopulation=250, lastPopulation=110,
                          births=450, deaths=590, history=[{}, {}], energyResidual=.01, matterResidual=.02, oxygenResidual=.03)
        else:
            report.update(deathFrames=81, deathSequence=20, deathIdentity='test-death', deathCause='Starvation',
                          movement=[dict(form=form, frames=40, firstTick=20, lastTick=60,
                                         start=dict(x=0, y=0, z=0), end=dict(x=1, y=0, z=0))
                                    for form in ('Amoeboid', 'Ciliate', 'Spindle')])
        return report

    def check(self, mode, report, duplicate=False):
        name = 'ecology.json' if mode == 'ecology' else 'living-cells.json'
        (self.root / name).write_text(json.dumps(report))
        names = ECOLOGY if mode == 'ecology' else LIVING
        def raw(path):
            i = 0 if duplicate else names.index(path.name)
            return b'\x89PNG\r\n\x1a\n' + b'\0' * 8 + struct.pack('>II', 1920, 1080) + bytes([i % 256]) * 24000
        with patch.object(Path, 'read_bytes', raw):
            return validate(self.root, mode, 'test-sha')

    def test_complete_metadata(self):
        for mode in ('ecology', 'living'):
            self.check(mode, self.sample(mode))

    def test_wrong_revision_fixture_null_device_and_unrendered_camera(self):
        for mode in ('ecology', 'living'):
            for mutation in ('revision', 'fixture', 'device', 'camera', 'missing', 'flat'):
                e = self.sample(mode)
                if mutation == 'revision': e['revision'] = 'older'
                if mutation == 'fixture': e['fixture'] = True
                if mutation == 'device': e['render']['graphicsApi'] = 'Null'
                if mutation == 'camera': e['render']['screenshots'][0]['cameraFrames'] = 0
                if mutation == 'missing': e['render']['screenshots'].pop()
                if mutation == 'flat': e['render']['screenshots'][0]['luminanceDeviation'] = 0
                with self.subTest(mode=mode, mutation=mutation), self.assertRaises(ValueError):
                    self.check(mode, e)

    def test_ecology_requires_time_population_history_and_correct_layers(self):
        for mutation in ('time', 'population', 'history', 'layer', 'ledger'):
            e = self.sample('ecology')
            if mutation == 'time': e['lastTick'] = 12019
            if mutation == 'population': e['lastPopulation'] = 0
            if mutation == 'history': e['history'] = []
            if mutation == 'layer': e['render']['screenshots'][0]['surveyLayer'] = 2
            if mutation == 'ledger': e['energyResidual'] = float('nan')
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                self.check('ecology', e)

    def test_living_requires_displacement_model_ticks_and_real_death(self):
        for mutation in ('displacement', 'ticks', 'death'):
            e = self.sample('living')
            if mutation == 'displacement': e['movement'][0]['end'] = copy.copy(e['movement'][0]['start'])
            if mutation == 'ticks': e['movement'][0]['lastTick'] = 59
            if mutation == 'death': e['deathSequence'] = 0
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                self.check('living', e)

    def test_reject_static_sequences(self):
        for mode in ('ecology', 'living'):
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                self.check(mode, self.sample(mode), duplicate=True)


if __name__ == '__main__':
    unittest.main()
