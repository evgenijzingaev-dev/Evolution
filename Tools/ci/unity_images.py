"""Resolve GameCI images without changing the project's Unity version or logging auth."""
import json
import os
import re
import urllib.error
import urllib.request
from pathlib import Path


def get_json(url, headers=None):
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers or {}), timeout=30) as response:
        return json.load(response), response.headers


def resolve_image(version, module, token):
    tag = f"ubuntu-{version}-{module}-3"
    try:
        manifest, headers = get_json(
            f"https://registry-1.docker.io/v2/unityci/editor/manifests/{tag}",
            {"Authorization": f"Bearer {token}", "Accept": "application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.docker.distribution.manifest.v2+json"},
        )
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise RuntimeError(f"Image registry returned HTTP {error.code} for {tag}") from None
    if "manifests" in manifest and not any(
        item.get("platform", {}).get("os") == "linux"
        and item.get("platform", {}).get("architecture") == "amd64"
        for item in manifest["manifests"]
    ):
        raise RuntimeError(f"No Linux amd64 image in {tag}")
    digest = headers.get("Docker-Content-Digest", "")
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise RuntimeError(f"Missing image digest for {tag}")
    print(f"GameCI {tag}: {digest}")
    return f"unityci/editor@{digest}"


def main():
    project_version = Path("ProjectSettings/ProjectVersion.txt").read_text()
    match = re.search(r"^m_EditorVersion: (\d+\.\d+\.\d+[abfp]\d+)\s*$", project_version, re.MULTILINE)
    if not match:
        raise RuntimeError("Cannot read exact Unity version from ProjectVersion.txt")
    version = match[1]
    auth, _ = get_json("https://auth.docker.io/token?service=registry.docker.io&scope=repository:unityci/editor:pull")
    editor = resolve_image(version, "base", auth["token"])
    if editor is None:
        raise RuntimeError(f"GameCI editor image unavailable for Unity {version}; no automatic downgrade")
    build = resolve_image(version, "linux-il2cpp", auth["token"])
    outputs = {"version": version, "editor_image": editor, "build_image": build or "", "build_supported": str(build is not None).lower()}
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a") as output:
            output.writelines(f"{key}={value}\n" for key, value in outputs.items())
    print(f"Unity {version}; Linux build image available: {build is not None}. Editor/tests have not run yet.")


if __name__ == "__main__":
    main()
