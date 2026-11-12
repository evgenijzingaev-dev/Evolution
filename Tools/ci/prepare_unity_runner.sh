#!/usr/bin/env bash
set -euo pipefail

# Never apply this cleanup to a developer machine or a persistent self-hosted runner.
if [[ "${GITHUB_ACTIONS:-}" != true || "${RUNNER_ENVIRONMENT:-}" != github-hosted || "${RUNNER_OS:-}" != Linux ]]; then
  echo '::error::Unity disk cleanup is restricted to disposable GitHub-hosted Linux runners.'
  exit 1
fi

docker_root=$(docker info --format '{{.DockerRootDir}}')
echo 'Disk space before cleanup:'
df -h / "$GITHUB_WORKSPACE" "$docker_root"
available_before=$(df -PB1 "$docker_root" | awk 'NR == 2 {print $4}')

# Fixed SDK paths only. Unity's SDK/toolchain lives inside its Docker image.
# .NET tests run in a separate, unchanged job on a separate VM.
# Keep Docker, Git, system Python, Node used by Actions, runner tools and workspace.
unused_sdks=(
  /usr/local/lib/android
  /usr/share/dotnet
  /usr/share/swift
  /usr/local/.ghcup
  /opt/ghc
  /opt/hostedtoolcache/CodeQL
  /usr/local/share/powershell
)
sudo rm -rf --one-file-system -- "${unused_sdks[@]}"
sudo apt-get clean

# This runs before the first Unity pull. Never prune volumes or active containers.
docker image prune --all --force
docker builder prune --all --force

echo 'Disk space after cleanup:'
df -h / "$GITHUB_WORKSPACE" "$docker_root"
available_after=$(df -PB1 "$docker_root" | awk 'NR == 2 {print $4}')
gib=$((1024 * 1024 * 1024))
minimum_bytes=$((30 * gib))
{
  echo '### Unity runner disk space'
  echo "Docker filesystem available before cleanup: $((available_before / gib)) GiB."
  echo "Docker filesystem available after cleanup: $((available_after / gib)) GiB."
  echo "Reclaimed: $(((available_after - available_before) / gib)) GiB."
} >> "$GITHUB_STEP_SUMMARY"

if (( available_after < minimum_bytes )); then
  echo '::error::Less than 30 GiB available for Unity image extraction, Library and IL2CPP build.'
  exit 1
fi
