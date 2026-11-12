"""Validate ONLY Unity-produced evidence; never create or repair screenshots."""
import hashlib
import json
import os
import struct
import sys
from pathlib import Path

EXPECTED = ["01-ocean.png", "02-cell-close.png", "03-depth-ensemble.png", "04-currents.png", "05-temperature-food.png"]


def validate(directory, revision=None):
    directory = Path(directory)
    data = json.loads((directory / "manifest.json").read_text())
    if data["unity"] != "6000.6.9f1" or data["pipeline"] != "UniversalRenderPipelineAsset":
        raise ValueError("Wrong Unity version/pipeline")
    if data["graphicsApi"] == "Null" or not data["graphicsDevice"]:
        raise ValueError("No rendering device")
    if data["scene"] != "WaterPrototype3D" or (data["width"], data["height"], data["seed"]) != (1920, 1080, 42):
        raise ValueError("Wrong scene, resolution or seed")
    if revision and data["revision"] != revision:
        raise ValueError("Preview belongs to another revision")
    if [s["file"] for s in data["screenshots"]] != EXPECTED:
        raise ValueError("Five ordered screenshots required")
    hashes = set()
    for shot in data["screenshots"]:
        image = (directory / shot["file"]).read_bytes()
        if image[:8] != b"\x89PNG\r\n\x1a\n" or image[12:16] != b"IHDR" or struct.unpack(">II", image[16:24]) != (1920, 1080):
            raise ValueError("Invalid PNG/resolution")
        if len(image) < 20000 or shot["luminanceRange"] <= .12 or shot["luminanceDeviation"] <= .015:
            raise ValueError("Empty/flat image")
        hashes.add(hashlib.sha256(image).hexdigest())
        if shot["tick"] < 20:
            raise ValueError("Simulation did not initialize")
    if len(hashes) != 5:
        raise ValueError("Duplicate views")
    ensemble, flow, food = data["screenshots"][2:]
    if ensemble["visibleOrganisms"] < 3 or ensemble["farthestDepth"] - ensemble["nearestDepth"] < 3:
        raise ValueError("Missing real depth ensemble")
    if not flow["currents"] or flow["currentSpeed"] <= 0:
        raise ValueError("Missing current layer/data")
    if not food["temperature"] or not food["food"] or food["foodSubstrate"] + food["foodOrganic"] <= 0:
        raise ValueError("Missing heat/food layer/data")
    return data


if __name__ == "__main__":
    report = validate(sys.argv[1], os.getenv("EVOLUTION_PREVIEW_SHA"))
    print(f"Five real game-frame captures validated: Unity {report['unity']}, {report['graphicsApi']}, {report['graphicsDevice']}.")
    if os.getenv("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as summary:
            summary.write("\nFive Unity game-frame captures passed structural checks (not an artistic approval).\n")
            summary.write(f"\nUnity {report['unity']}; API {report['graphicsApi']}; device {report['graphicsDevice']}.\n")
