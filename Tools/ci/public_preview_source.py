"""Resolve a trusted, successfully uploaded native Unity artifact via GitHub's API."""
import argparse
import json
import os
import urllib.parse
import urllib.request
from pathlib import Path

from public_visual_previews import REPOSITORY, number, trusted_branch


def resolve(get, run_id, pr_number=0):
    run_id = number(run_id)
    repo = get("")
    if repo["full_name"] != REPOSITORY:
        raise ValueError("Unexpected source repository")
    if repo["private"]:
        # Do not change visibility. Publication stays unconfirmed until every raw
        # image passes the separate anonymous HTTP/content verification on runner.
        print("Repository is PRIVATE; anonymous raw image access is not assumed. "
              "All 12 URLs must pass verification before any success comment.")
    run = get(f"/actions/runs/{run_id}")
    if (run["path"] != ".github/workflows/visual-preview.yml" or
            run["head_repository"]["full_name"] != REPOSITORY or
            not trusted_branch(run["head_branch"]) or
            run["event"] not in ("pull_request", "workflow_dispatch")):
        raise ValueError("Untrusted render source")
    # The calling workflow can still be in progress. The render job itself MUST be done.
    jobs = get(f"/actions/runs/{run_id}/jobs?filter=latest&per_page=100")["jobs"]
    render = [job for job in jobs if job["name"] == "render"]
    if len(render) != 1 or render[0]["conclusion"] != "success" or not any(
            step["name"] == "Upload validated visual evidence" and step["conclusion"] == "success"
            for step in render[0]["steps"]):
        raise ValueError("No successful render and validated artifact upload")
    if not pr_number:
        candidates = [p["number"] for p in run["pull_requests"]]
        if not candidates:
            owner = REPOSITORY.split("/")[0]
            query = urllib.parse.urlencode({"state": "open", "head": owner + ":" + run["head_branch"], "per_page": 100})
            candidates = [p["number"] for p in get("/pulls?" + query)]
        if len(candidates) != 1:
            raise ValueError("Specify the unique destination PR explicitly")
        pr_number = candidates[0]
    pr_number = number(pr_number)
    pr = get(f"/pulls/{pr_number}")
    if (pr["head"]["repo"]["full_name"] != REPOSITORY or
            pr["head"]["ref"] != run["head_branch"] or
            not trusted_branch(pr["head"]["ref"])):
        raise ValueError("Fork or unrelated destination PR")
    associated = any(p["number"] == pr_number for p in run["pull_requests"])
    if not associated and pr["head"]["sha"] != run["head_sha"]:
        commits = []
        for page in range(1, 4):
            batch = get(f"/pulls/{pr_number}/commits?per_page=100&page={page}")
            commits.extend(item["sha"] for item in batch)
            if len(batch) < 100:
                break
        if run["head_sha"] not in commits:
            raise ValueError("Render SHA is not associated with the destination PR")
    attempt = number(run["run_attempt"])
    name = f"water-visual-preview-{run_id}-{attempt}"
    artifacts = get(f"/actions/runs/{run_id}/artifacts?per_page=100")["artifacts"]
    matches = [a for a in artifacts if a["name"] == name and not a["expired"]]
    if len(matches) != 1:
        raise ValueError("Exactly one unexpired validated artifact is required")
    artifact_id = number(matches[0]["id"])
    return dict(repository=REPOSITORY, head_branch=run["head_branch"], pr=pr_number,
                source_run=run_id, source_attempt=attempt, source_sha=run["head_sha"],
                artifact_name=name, artifact_id=artifact_id,
                source_artifact=f"https://github.com/{REPOSITORY}/actions/runs/{run_id}/artifacts/{artifact_id}")


def get_api(path):
    request = urllib.request.Request("https://api.github.com/repos/" + REPOSITORY + path, headers={
        "Authorization": "Bearer " + os.environ["GH_TOKEN"], "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "Evolution-Preview-Publisher"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", type=int, default=0)
    parser.add_argument("--pr", type=int, default=0)
    parser.add_argument("--request", default="Tools/ci/public_preview_request.json")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    if not args.run:
        request = json.loads(Path(args.request).read_text())
        args.run, args.pr = number(request["source_run"]), number(request["pr"])
    try:
        plan = resolve(get_api, args.run, args.pr)
    except ValueError as error:
        if os.getenv("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as summary:
                summary.write("## Public preview publication blocked\n\n" + str(error) +
                              "\n\nOriginal Unity PNG artifacts are unchanged. No public URLs were announced.\n")
        raise
    Path(args.output).write_text(json.dumps(plan, indent=2) + "\n")
    with open(os.environ["GITHUB_OUTPUT"], "a") as output:
        output.write(f"artifact_name={plan['artifact_name']}\nrun_id={plan['source_run']}\n")
    print(f"Validated native Unity source run {plan['source_run']} for PR {plan['pr']}")


if __name__ == "__main__":
    main()
