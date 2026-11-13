"""Validate ONLY Unity-produced evidence; never create or repair screenshots."""
import hashlib
import json
import os
import struct
import sys
from pathlib import Path

EXPECTED = ["01-ocean.png", "02-cell-close.png", "03-depth-ensemble.png", "04-currents.png", "05-temperature-food.png", "07-cinematic-overview.png", "08-analytical-overview.png", "09-density-map.png", "10-depth-distribution.png", "11-selected-organism.png", "12-horizontal-slice.png", "13-cell-play-ui.png", "14-cell-info-ui.png", "15-cinematic-ui.png", "16-analytical-ui.png", "17-danger-food-ui.png", "18-scientific-ui.png", "19-ui-1366x768.png", "20-ui-2560x1440.png", "21-development-ui.png"]


def validate(directory, revision=None):
    directory = Path(directory)
    data = json.loads((directory / "manifest.json").read_text())
    if data.get("passed") is not True:
        raise ValueError("Diagnostic capture is not a validated preview")
    if data["unity"] != "6000.6.9f1" or data["pipeline"] != "UniversalRenderPipelineAsset":
        raise ValueError("Wrong Unity version/pipeline")
    if data["graphicsApi"] == "Null" or not data["graphicsDevice"]:
        raise ValueError("No rendering device")
    if data["scene"] != "WaterPrototype3D" or (data["width"], data["height"], data["seed"]) != (1920, 1080, 42):
        raise ValueError("Wrong scene, resolution or seed")
    if revision and data["revision"] != revision:
        raise ValueError("Preview belongs to another revision")
    if [s["file"] for s in data["screenshots"]] != EXPECTED:
        raise ValueError("Twenty ordered screenshots required")
    hashes = set()
    for shot in data["screenshots"]:
        image = (directory / shot["file"]).read_bytes()
        if image[:8] != b"\x89PNG\r\n\x1a\n" or image[12:16] != b"IHDR" or struct.unpack(">II", image[16:24]) != ((1366,768) if shot["file"]=="19-ui-1366x768.png" else (2560,1440) if shot["file"]=="20-ui-2560x1440.png" else (1920,1080)):
            raise ValueError("Invalid PNG/resolution")
        if len(image) < 20000 or shot["luminanceRange"] <= .12 or shot["luminanceDeviation"] <= .005:
            raise ValueError("Empty/flat image")
        hashes.add(hashlib.sha256(image).hexdigest())
        if shot["tick"] < 20:
            raise ValueError("Simulation did not initialize")
        if shot["cameraFrames"] < 2:
            raise ValueError("URP camera did not render")
    if len(hashes) != len(EXPECTED):
        raise ValueError("Duplicate views")
    ensemble, flow, food = data["screenshots"][2:5]
    if not flow["hudVisible"] or not food["hudVisible"]:
        raise ValueError("Diagnostic views must include the existing Russian HUD")
    if ensemble["visibleOrganisms"] < 3 or ensemble["farthestDepth"] - ensemble["nearestDepth"] < 3:
        raise ValueError("Missing real depth ensemble")
    if not flow["currents"] or flow["currentSpeed"] <= 0:
        raise ValueError("Missing current layer/data")
    if not food["temperature"] or not food["food"] or food["foodSubstrate"] + food["foodOrganic"] <= 0:
        raise ValueError("Missing heat/food layer/data")
    if not data.get("navigationChecked"):
        raise ValueError("Camera return/orbit/cell play checks did not run")
    cinematic, analytical, density, depth, chosen, sliced = data["screenshots"][5:11]
    for frame in data["screenshots"][5:11]:
        if not frame["hudVisible"] or frame["activePopulation"] < 1 or frame["knownRegions"] < 1:
            raise ValueError("Missing actual overview data/HUD")
    if cinematic["analytical"] or not analytical["analytical"] or analytical["organismMarkers"] < 3:
        raise ValueError("Missing cinematic/analytical switch or markers")
    if not density["analytical"] or density["surveyLayer"] != 4 or density["regionClusters"] < 3:
        raise ValueError("Missing real density map")
    if not depth["depthColours"] or depth["markerMaxY"]-depth["markerMinY"] < 10:
        raise ValueError("Missing vertical distribution")
    if not chosen["selectedOrganism"] or chosen["analytical"]:
        raise ValueError("Missing selected actual organism")
    if sliced["fullDepth"] or sliced["organismMarkers"] < 1 or sliced["markerMinY"] < sliced["sliceIndex"]*20-.01 or sliced["markerMaxY"] > (sliced["sliceIndex"]+1)*20+.01:
        raise ValueError("Wrong horizontal slice")
    hud=data["screenshots"][11:]
    for shot in hud:
        if not shot.get("friendlyHud") or not shot.get("layoutFits"):
            raise ValueError("Missing friendly HUD or invalid layout")
    if hud[0]["hudPage"]!=0 or hud[2]["hudPage"]!=1 or hud[-1]["hudPage"]!=2:
        raise ValueError("HUD navigation missing")
    if hud[5]["hudDetail"]!=2 or hud[6]["hudDetail"]!=0:
        raise ValueError("HUD detail levels missing")
    # Absence of an actual nearby predator must be reported, never disguised as success.
    data["dangerCaptureAvailable"]=hud[4].get("threatIndex",-1)>=0
    return data


if __name__ == "__main__":
    report = validate(sys.argv[1], os.getenv("EVOLUTION_PREVIEW_SHA"))
    print(f"Twenty real game-frame captures validated: Unity {report['unity']}, {report['graphicsApi']}, {report['graphicsDevice']}.")
    if os.getenv("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as summary:
            summary.write("\nTwenty Unity game-frame captures passed structural checks (not an artistic approval).\n")
            summary.write(f"\nUnity {report['unity']}; API {report['graphicsApi']}; device {report['graphicsDevice']}.\n")

