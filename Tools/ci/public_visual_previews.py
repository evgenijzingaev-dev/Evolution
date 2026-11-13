"""Publish bounded, metadata-free JPEG derivatives of validated Unity screenshots."""
import argparse
import base64
import hashlib
import io
import json
import os
import re
import subprocess
import tempfile
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image
from check_visual_preview import validate

REPOSITORY = "evgenijzingaev-dev/Evolution"
REMOTE = "https://github.com/" + REPOSITORY + ".git"
REF = "refs/heads/visual-previews"
RAW = "https://raw.githubusercontent.com/" + REPOSITORY + "/visual-previews/"
FRAMES = (
    "01-ocean", "02-cell-close", "03-depth-ensemble", "04-currents",
    "05-temperature-food", "06-water-only", "07-cinematic-overview",
    "08-analytical-overview", "09-density-map", "10-depth-distribution",
    "11-selected-organism", "12-horizontal-slice",
)
MAX_PRS, MAX_IMAGE, MAX_TOTAL = 8, 350 * 1024, 32 * 1024 * 1024
Image.MAX_IMAGE_PIXELS = 4_000_000
META_KEYS = {"schema", "repository", "pr", "source_run", "source_attempt", "source_sha",
             "unity", "pipeline", "source_artifact", "published_at", "images"}
IMAGE_KEYS = {"file", "width", "height", "sha256", "original_sha256"}


def trusted_branch(name):
    return name == "main" or bool(re.fullmatch(r"codex/[A-Za-z0-9_./-]+", name or ""))


def number(value):
    if not re.fullmatch(r"[1-9][0-9]{0,12}", str(value)):
        raise ValueError("Positive numeric PR/run identifier required")
    return int(value)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode()


def urls(meta):
    return [RAW + f"pr-{meta['pr']}/" + image["file"] for image in meta["images"]]


def index(meta):
    lines = [f"# Visual Preview — PR #{meta['pr']}", "",
             f"Источник: Unity {meta['unity']} / URP, `{meta['source_sha']}`.", "",
             f"[Оригинальные PNG в Actions Artifacts]({meta['source_artifact']})", ""]
    lines.extend(f"- [{item['file']}]({url})" for item, url in zip(meta["images"], urls(meta)))
    return ("\n".join(lines) + "\n").encode()


def encode_frame(png):
    if len(png) > 8 * 1024 * 1024:
        raise ValueError("Oversized source frame")
    with Image.open(io.BytesIO(png)) as image:
        if image.format != "PNG" or image.size != (1920, 1080):
            raise ValueError("Expected actual 1920x1080 Unity PNG")
        image.load()
        pixels = image.convert("RGB")
        for width, quality in [(1440, 82), (1440, 74), (1440, 66), (1280, 66)]:
            # Fresh pixels and empty metadata: no PNG text, EXIF, ICC or source paths.
            small = pixels.resize((width, width * 1080 // 1920), Image.Resampling.LANCZOS)
            small.info.clear()
            output = io.BytesIO()
            small.save(output, "JPEG", quality=quality, optimize=True, progressive=True, exif=b"")
            data = output.getvalue()
            if len(data) <= MAX_IMAGE:
                return data, small.size
    raise ValueError("Preview cannot fit the per-image storage budget")


def prepare(originals, plan):
    originals = Path(originals)
    expected = {name + ".png" for name in FRAMES} | {"manifest.json"}
    entries = list(originals.iterdir())
    if {p.name for p in entries} != expected or any(p.is_symlink() or not p.is_file() for p in entries):
        raise ValueError("Original artifact must contain exactly 12 PNGs and the capture manifest")
    if sum(p.stat().st_size for p in entries) > 64 * 1024 * 1024:
        raise ValueError("Original artifact exceeds the size limit")
    report = validate(originals, plan["source_sha"])
    meta = dict(schema=1, repository=REPOSITORY, pr=number(plan["pr"]),
                source_run=number(plan["source_run"]), source_attempt=number(plan["source_attempt"]),
                source_sha=plan["source_sha"], unity=report["unity"], pipeline=report["pipeline"],
                source_artifact=plan["source_artifact"],
                published_at=datetime.now(timezone.utc).isoformat(timespec="seconds"), images=[])
    files = {}
    for name in FRAMES:
        png = (originals / (name + ".png")).read_bytes()
        jpg, (width, height) = encode_frame(png)
        filename = name + ".jpg"
        files[filename] = jpg
        meta["images"].append(dict(file=filename, width=width, height=height,
                                   sha256=digest(jpg), original_sha256=digest(png)))
    check_meta(meta)
    files["publication.json"] = json_bytes(meta)
    files["index.md"] = index(meta)
    return meta, files


def check_meta(meta):
    if set(meta) != META_KEYS or meta["schema"] != 1 or meta["repository"] != REPOSITORY:
        raise ValueError("Unsafe publication metadata")
    number(meta["pr"]); number(meta["source_run"]); number(meta["source_attempt"])
    if not re.fullmatch(r"[0-9a-f]{40}", meta["source_sha"]):
        raise ValueError("Invalid source commit")
    if meta["unity"] != "6000.6.9f1" or meta["pipeline"] != "UniversalRenderPipelineAsset":
        raise ValueError("Wrong Unity/URP provenance")
    if not re.fullmatch(r"https://github\.com/evgenijzingaev-dev/Evolution/actions/runs/[0-9]+/artifacts/[0-9]+", meta["source_artifact"]):
        raise ValueError("Unsafe original artifact URL")
    datetime.fromisoformat(meta["published_at"])
    if [i.get("file") for i in meta["images"]] != [n + ".jpg" for n in FRAMES]:
        raise ValueError("Unexpected preview filenames")
    for item in meta["images"]:
        if set(item) != IMAGE_KEYS or item["width"] not in (1280, 1440) or item["height"] != item["width"] * 1080 // 1920:
            raise ValueError("Unsafe image metadata")
        if any(not re.fullmatch(r"[0-9a-f]{64}", item[key]) for key in ("sha256", "original_sha256")):
            raise ValueError("Invalid image fingerprint")


def git_env():
    env = os.environ.copy()
    env.update(GIT_AUTHOR_NAME="Evolution Visual Preview", GIT_COMMITTER_NAME="Evolution Visual Preview",
               GIT_AUTHOR_EMAIL="41898282+github-actions[bot]@users.noreply.github.com",
               GIT_COMMITTER_EMAIL="41898282+github-actions[bot]@users.noreply.github.com")
    token = os.getenv("GH_TOKEN")
    if token:
        # Pass authentication through the environment, never command arguments or logs.
        env["GIT_CONFIG_COUNT"] = "1"
        env["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader"
        env["GIT_CONFIG_VALUE_0"] = "AUTHORIZATION: basic " + base64.b64encode(("x-access-token:" + token).encode()).decode()
    return env


def git(directory, *args, data=None, check=True):
    result = subprocess.run(["git", "-C", str(directory), *args], input=data, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, env=git_env(), timeout=90)
    if check and result.returncode:
        raise RuntimeError("Git preview operation failed; check branch permissions or lease (credentials suppressed)")
    return result


def read_snapshot(directory, remote):
    listing = git(directory, "ls-remote", "--heads", remote, REF).stdout.decode().split()
    if not listing:
        return "", {}
    old = listing[0]
    git(directory, "fetch", "--depth=1", "--no-tags", remote, REF)
    # A concurrent update between ls-remote/fetch must not be silently overwritten.
    fetched = git(directory, "rev-parse", "FETCH_HEAD").stdout.decode().strip()
    if fetched != old:
        raise RuntimeError("Preview ref changed during fetch; retry publication")
    files = {}
    allowed = set(n + ".jpg" for n in FRAMES) | {"publication.json", "index.md"}
    for record in git(directory, "ls-tree", "-r", "-z", fetched).stdout.split(b"\0"):
        if not record:
            continue
        entry, name = record.split(b"\t", 1)
        mode, kind, oid = entry.decode().split()
        path = name.decode("utf-8")
        parts = path.split("/")
        if mode != "100644" or kind != "blob" or not (path == "index.md" or
                len(parts) == 2 and re.fullmatch(r"pr-[1-9][0-9]{0,12}", parts[0]) and parts[1] in allowed):
            raise ValueError("Unexpected file in preview branch; refusing to rewrite it")
        size = int(git(directory, "cat-file", "-s", oid).stdout)
        if size > MAX_IMAGE + 65536 or sum(map(len, files.values())) + size > MAX_TOTAL * 2:
            raise ValueError("Existing preview branch exceeds input limits")
        files[path] = git(directory, "cat-file", "blob", oid).stdout
    return old, files


def groups_from(files):
    groups = {}
    for path, content in files.items():
        if path != "index.md":
            folder, name = path.split("/")
            groups.setdefault(folder, {})[name] = content
    for folder, group in groups.items():
        meta = json.loads(group["publication.json"])
        check_meta(meta)
        if folder != f"pr-{meta['pr']}" or set(group) != {n + ".jpg" for n in FRAMES} | {"publication.json", "index.md"}:
            raise ValueError("Incomplete existing PR preview")
        if group["index.md"] != index(meta):
            raise ValueError("Unexpected existing preview index")
        for item in meta["images"]:
            data = group[item["file"]]
            if len(data) > MAX_IMAGE or digest(data) != item["sha256"]:
                raise ValueError("Existing image fingerprint mismatch")
            with Image.open(io.BytesIO(data)) as image:
                if image.format != "JPEG" or image.size != (item["width"], item["height"]) or image.getexif():
                    raise ValueError("Invalid existing JPEG")
    return groups


def combine(existing, meta, new_files):
    groups = groups_from(existing)
    folder = f"pr-{meta['pr']}"
    if folder in groups:
        old = json.loads(groups[folder]["publication.json"])
        if (old["source_run"], old["source_attempt"]) > (meta["source_run"], meta["source_attempt"]):
            return existing, old, "superseded"
        if old["source_run"] == meta["source_run"] and old["source_attempt"] == meta["source_attempt"] and old["images"] == meta["images"]:
            meta = dict(meta, published_at=old["published_at"])
            new_files = dict(new_files, **{"publication.json": json_bytes(meta), "index.md": index(meta)})
    groups[folder] = new_files
    ordered = sorted((key for key in groups if key != folder),
                     key=lambda key: json.loads(groups[key]["publication.json"])["published_at"], reverse=True)
    kept = [folder] + ordered[:MAX_PRS-1]
    while sum(len(v) for key in kept for v in groups[key].values()) > MAX_TOTAL - 65536:
        if len(kept) == 1:
            raise ValueError("Current preview exceeds total storage budget")
        kept.pop()
    files = {key + "/" + name: value for key in kept for name, value in groups[key].items()}
    root = ["# EVOLUTION — публичные превью Unity", "", f"Последние {MAX_PRS} PR; по одному актуальному набору на PR.", ""]
    root += [f"- [PR #{key[3:]}]({RAW}{key}/index.md)" for key in kept]
    files["index.md"] = ("\n".join(root) + "\n").encode()
    return files, meta, "updated" if files != existing else "unchanged"


def tree(directory, files):
    folders = {}
    root = []
    for path, content in sorted(files.items()):
        blob = git(directory, "hash-object", "-w", "--stdin", data=content).stdout.decode().strip()
        if "/" in path:
            folder, name = path.split("/")
            folders.setdefault(folder, []).append(f"100644 blob {blob}\t{name}\0")
        else:
            root.append(f"100644 blob {blob}\t{path}\0")
    for folder, entries in folders.items():
        oid = git(directory, "mktree", "-z", data="".join(entries).encode()).stdout.decode().strip()
        root.append(f"040000 tree {oid}\t{folder}\0")
    return git(directory, "mktree", "-z", data="".join(sorted(root)).encode()).stdout.decode().strip()


def publish_snapshot(meta, prepared, remote=REMOTE):
    with tempfile.TemporaryDirectory(prefix="evolution-public-preview-") as directory:
        git(directory, "init", "--bare", "--quiet")
        for attempt in range(5):
            old, existing = read_snapshot(directory, remote)
            files, selected, status = combine(existing, meta, prepared)
            if status in ("unchanged", "superseded"):
                return selected, status
            oid = tree(directory, files)
            # No parent: obsolete binary snapshots cannot accumulate in reachable branch history.
            commit = git(directory, "commit-tree", oid, "-m",
                         f"PR {meta['pr']} Unity previews from run {meta['source_run']}").stdout.decode().strip()
            result = git(directory, "push", remote, f"{commit}:{REF}", f"--force-with-lease={REF}:{old}", check=False)
            if result.returncode == 0:
                return selected, status
            current = git(directory, "ls-remote", "--heads", remote, REF).stdout.decode().split()
            if (current[0] if current else "") == old:
                raise RuntimeError("Preview branch push rejected; check contents:write / branch rules")
        raise RuntimeError("Concurrent preview updates exhausted lease retries")


def fetch_public(url):
    # Deliberately anonymous: no Authorization header, API token or Git credential.
    request = urllib.request.Request(url, headers={"User-Agent": "Evolution-Preview-Check", "Cache-Control": "no-cache"})
    with urllib.request.urlopen(request, timeout=15) as response:
        if response.status != 200 or response.headers.get_content_type() != "image/jpeg":
            raise ValueError("Public URL is not HTTP 200 image/jpeg")
        data = response.read(MAX_IMAGE + 1)
    if len(data) > MAX_IMAGE:
        raise ValueError("Public image exceeds budget")
    return data


def verify_public(meta, deadline=300, fetch=fetch_public):
    pending = dict(zip(urls(meta), meta["images"]))
    end = time.monotonic() + deadline
    while pending:
        def check(pair):
            url, item = pair
            try:
                data = fetch(url)
                if digest(data) == item["sha256"]:
                    with Image.open(io.BytesIO(data)) as image:
                        image.load()
                        return url if image.format == "JPEG" and image.size == (item["width"], item["height"]) else None
            except Exception:
                return None
        with ThreadPoolExecutor(max_workers=4) as pool:
            for url in pool.map(check, list(pending.items())):
                if url:
                    pending.pop(url)
        if pending:
            if time.monotonic() >= end:
                raise RuntimeError("Public URL verification failed; no success report may be posted")
            time.sleep(10)
    return urls(meta)


def comment(meta):
    lines = ["<!-- evolution-public-visual-preview -->", "## Публичные кадры Unity / URP", "",
             f"Источник: `{meta['source_sha']}` · Unity {meta['unity']} · PR #{meta['pr']}.", "",
             "Все 12 ссылок проверены анонимным HTTPS GET: HTTP 200, JPEG и совпадение SHA-256.", "",
             f"[Индекс]({RAW}pr-{meta['pr']}/index.md) · [Оригинальные PNG]({meta['source_artifact']})", ""]
    lines += [f"- [{item['file']}]({url})" for item, url in zip(meta["images"], urls(meta))]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--originals", required=True)
    parser.add_argument("--plan", required=True)
    args = parser.parse_args()
    plan = json.loads(Path(args.plan).read_text())
    if plan["repository"] != REPOSITORY or not trusted_branch(plan["head_branch"]):
        raise ValueError("Untrusted source repository/branch")
    meta, files = prepare(args.originals, plan)
    meta, status = publish_snapshot(meta, files)
    verify_public(meta)
    body = comment(meta)
    if os.getenv("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a") as output:
            output.write(f"pr={meta['pr']}\nstatus={status}\n")
            output.write("comment_json=" + json.dumps({"body": body}, ensure_ascii=False) + "\n")
    if os.getenv("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as summary:
            summary.write(body)
    print(f"Public preview {status}: 12 anonymous HTTPS image checks passed for PR {meta['pr']}.")


if __name__ == "__main__":
    main()
