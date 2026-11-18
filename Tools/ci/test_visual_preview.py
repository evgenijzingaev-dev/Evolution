"""Small evidence-validator unit tests. Synthetic bytes stay in temporary test data,
never in the Unity capture directory or uploaded artifacts.
"""
import copy
import json
import struct
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from check_visual_preview import EXPECTED, validate
from select_visual_preview import visual_change


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        shot = dict(tick=20, cameraFrames=4, luminanceRange=.8, luminanceDeviation=.2,
                    visibleOrganisms=5, nearestDepth=4, farthestDepth=20,
                    currents=True, currentSpeed=.2, temperature=True, food=True, hudVisible=True,
                    foodSubstrate=12, foodOrganic=4)
        self.report = dict(passed=True, unity="6000.6.9f1", pipeline="UniversalRenderPipelineAsset",
                           graphicsApi="OpenGLCore", graphicsDevice="test-device",
                           scene="WaterPrototype3D", width=1920, height=1080, seed=42,
                           revision="test-sha", screenshots=[dict(shot, file=f) for f in EXPECTED])
        self.images = {f: b"\x89PNG\r\n\x1a\n" + struct.pack(">I", 13) + b"IHDR" + struct.pack(">II", 1920, 1080) + bytes([i]) * 24000
                       for i, f in enumerate(EXPECTED)}

    def check(self):
        (self.root / "manifest.json").write_text(json.dumps(self.report))
        with patch.object(Path, "read_bytes", lambda p: self.images[p.name]):
            return validate(self.root, "test-sha")

    def test_complete_evidence(self):
        self.assertEqual(len(self.check()["screenshots"]), 5)

    def test_wrong_render_setup_or_revision(self):
        original = copy.deepcopy(self.report)
        for key, value in [("unity", "6000.0.1f1"), ("pipeline", "BuiltIn"), ("graphicsApi", "Null"),
                           ("revision", "older-sha"), ("scene", "CellPrototype")]:
            with self.subTest(key=key):
                self.report = dict(original, **{key: value})
                with self.assertRaises(ValueError): self.check()

    def test_missing_view(self):
        self.report["screenshots"].pop()
        with self.assertRaises(ValueError): self.check()

    def test_flat_frame(self):
        self.report["screenshots"][0]["luminanceDeviation"] = 0
        with self.assertRaises(ValueError): self.check()

    def test_sparse_ocean_is_not_an_empty_frame(self):
        self.report["screenshots"][0].update(luminanceRange=.7843059, luminanceDeviation=.0138378)
        self.check()

    def test_diagnostic_is_never_accepted_as_verified(self):
        self.report["passed"] = False
        with self.assertRaises(ValueError): self.check()

    def test_camera_must_have_rendered(self):
        self.report["screenshots"][0]["cameraFrames"] = 0
        with self.assertRaises(ValueError): self.check()

    def test_duplicate_frames(self):
        self.images[EXPECTED[1]] = self.images[EXPECTED[0]]
        with self.assertRaises(ValueError): self.check()

    def test_wrong_png(self):
        self.images[EXPECTED[0]] = b"not-an-image"
        with self.assertRaises(ValueError): self.check()

    def test_no_depth(self):
        self.report["screenshots"][2]["farthestDepth"] = 5
        with self.assertRaises(ValueError): self.check()

    def test_disabled_current_layer(self):
        self.report["screenshots"][3]["currents"] = False
        with self.assertRaises(ValueError): self.check()

    def test_diagnostic_view_must_show_hud(self):
        self.report["screenshots"][3]["hudVisible"] = False
        with self.assertRaises(ValueError): self.check()

    def test_no_real_food(self):
        self.report["screenshots"][4].update(foodSubstrate=0, foodOrganic=0)
        with self.assertRaises(ValueError): self.check()


class TriggerTests(unittest.TestCase):
    def test_small_nonvisual_push_skips_unity(self):
        self.assertFalse(visual_change(["Assets/Evolution/Core/Biology.cs", "docs/README.md"]))

    def test_visual_push_requests_unity(self):
        for path in ["Assets/Evolution/Runtime/Resources/VolumeBody.shader",
                     "Assets/Evolution/Runtime/CrystalMeshes.cs", "Tools/ci/check_visual_preview.py",
                     "Assets/Evolution/Presentation/CrystalForms.cs", "Assets/Evolution/Settings/PrototypeURP.asset",
                     "Assets/Evolution/Presentation/WaterOptics.cs",
                     "ProjectSettings/GraphicsSettings.asset", "Packages/manifest.json"]:
            with self.subTest(path=path): self.assertTrue(visual_change([path]))


if __name__ == "__main__":
    unittest.main()
