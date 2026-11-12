"""Cheap per-push gate: PR path filters alone compare the WHOLE PR, not the last push."""
import fnmatch
import json
import os
import subprocess

PATTERNS = (
    ".github/workflows/visual-preview.yml", "Tools/ci/*visual_preview*",
    "Assets/Evolution/Editor/VisualPreviewBuild.cs", "Assets/Evolution/Runtime/WaterVisualPreview.cs",
    "Assets/Evolution/Runtime/WaterPrototype.cs", "Assets/Evolution/Runtime/WaterCamera.cs",
    "Assets/Evolution/Runtime/VolumePainter.cs", "Assets/Evolution/Runtime/CrystalWaterLook.cs",
    "Assets/Evolution/Runtime/CrystalMeshes.cs", "Assets/Evolution/Runtime/MicroMeshes.cs",
    "Assets/Evolution/Runtime/Resources/**", "Assets/Evolution/Scenes/WaterPrototype3D.unity",
    "Packages/manifest.json", "ProjectSettings/**",
)


def visual_change(files):
    return any(fnmatch.fnmatchcase(path, pattern) for path in files for pattern in PATTERNS)


def main():
    with open(os.environ["GITHUB_EVENT_PATH"]) as source:
        event = json.load(source)
    # Adding the opt-in label or manually dispatching is an explicit request for a new preview.
    run = True
    if event.get("action") == "synchronize" and event.get("before"):
        result = subprocess.run(["git", "diff", "--name-only", event["before"],
                                 event["pull_request"]["head"]["sha"], "--"],
                                capture_output=True, text=True)
        # A deleted pre-force-push commit may no longer be fetchable. Render rather than silently miss it.
        if result.returncode == 0:
            run = visual_change(result.stdout.splitlines())
    with open(os.environ["GITHUB_OUTPUT"], "a") as output:
        output.write("run=" + str(run).lower() + "\n")
    print("Visual files changed or explicit preview requested." if run else "Only nonvisual files changed in this push; Unity is skipped.")


if __name__ == "__main__":
    main()
