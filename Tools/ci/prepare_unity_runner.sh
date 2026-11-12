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

# The standard runner has 8 GiB RAM. Give Editor/IL2CPP memory spikes room to swap
# while keeping at least 30 GiB available for the image and build. Keep existing swap.
echo 'Memory before adding Unity swap:'
free -h
swap_bytes=$((8 * gib))
if (( available_after < minimum_bytes + swap_bytes )); then
  echo '::error::Need 38 GiB after SDK cleanup to reserve 8 GiB swap and 30 GiB for Unity.'
  exit 1
fi
if [[ -e /mnt/evolution-unity-ci.swap ]]; then
  echo '::error::Refusing to overwrite an existing Unity swap file.'
  exit 1
fi
sudo fallocate -l "$swap_bytes" /mnt/evolution-unity-ci.swap
sudo chmod 600 /mnt/evolution-unity-ci.swap
sudo mkswap /mnt/evolution-unity-ci.swap
sudo swapon /mnt/evolution-unity-ci.swap
echo 'Memory and disk space after adding Unity swap:'
free -h
df -h / "$GITHUB_WORKSPACE" "$docker_root"
echo 'Added 8 GiB swap for Unity; existing swap preserved.' >> "$GITHUB_STEP_SUMMARY"
