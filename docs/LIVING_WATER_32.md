# Issue #32 — living unicellular presentation in WaterPrototype3D

## Review status

Draft PR #33 targets `codex/multicellular-evolution-3d`, PR #31 head
`564fd7a3c0c62a70248728cd12731f06618a8074`. This keeps Issue #30's actual
paid bonds, roles, colony controls and Save v5. PR #28/#29/#31 and main are not
modified. No auto-merge, autopilot, secrets, workflow changes or opt-in CI labels.

The supplied `4804.png` is the artistic reference: hydrated translucent tissue,
purple nuclei, differentiated moving appendages, connected fission and visible
cytoplasmic loss. It is **concept art, not evidence of this implementation**.

Implementation and model tests are ready for independent review. Unity Editor,
shader/API compilation, PlayMode, appearance and performance in Unity are **not
verified**. Unity is absent on this executor. No game, Editor or capture was
launched on the user's computer. The visual Definition of Done remains open
until a separately authorized Unity run and inspection of original frames.

## Checkpoints

| Series | Commit | Working scenarios | Boundary |
|---|---|---|---|
| A | `1fa346a2354fa663aa005af3c956bd3d3dbe4cdd` | Shared membrane/interior deformation; amoeboid extension and cytoplasmic lag; metachronal cilia and travelling flagellar waves; signed turns and propulsion-relative orientation | Visual quality and shader support unverified |
| B | `fd25260c0c8439ef96449155b85dcffdee90bf63` | Journal records only completed paid births; continuous shared envelope, two true nuclei, narrowing neck and two real inherited cells; persistent bond remains when Core retains it | Maximum 32 simultaneously animated pairs; no historical event replay on load |
| C | `2ce08fbe8ef068932f653efbf0296aec1dc66ab6` | True death snapshot, slowing appendages, loss of turgor, local membrane opening, decorative gel/bubbles/fragments and four-second dissolution | At most 32 death tails; only exhaustion/damage causes exist in the model |
| D | This report's revision | Read-only frame-rate tests, budgets, bounds/picking polish, actual-player capture route and two Unity render regressions | Unity render regressions and capture route are prepared, **not executed** |

## What changed and why

`WaterCellPose` is a pure, read-only projection. The membrane shader and CPU
organelle/appendage roots use the same deformation. The existing six inherited
silhouette directions remain; they are not three forced biological species.
Size, membrane compliance, pigments, plastid density, motor length and activity
still derive from real genes, growth, energy and specialization. Mixed motor
phenotypes remain possible because Speed and Uptake are independent traits.
These are art projections of existing mobility/uptake traits, not new abilities
or a claim that Core now models separate taxonomic motor genes.

The amoeboid front extends along smoothed actual propulsion relative to water,
with a delayed fluid shift and asymmetric side/rear contraction. It has no
feet or mandatory contact with an invisible floor. Cilia have a faster power
stroke, folded recovery and phase offsets between rings; the flagellum carries
a travelling wave. Real velocity, reserve, angular change and commanded effort
affect frequency/amplitude. Idle breathing, readiness swelling, feeding response
and internal granule flow remain subtle. None applies propulsion to Core.

Birth animation requires both real agents and a matching successful journal
entry. It does not run from a button press. One shared envelope grows into two
lobes before the actual parent/daughter shells become visible; daughters no
longer grow a finished miniature model out of nothing. Their energy, growth,
genome and final position remain Core's values. The real retained bond is drawn
independently and persists after the temporary fission neck disappears. Death
of either member ends the pair transition rather than leaving a phantom neck.

`LifeJournal` is a bounded transient sequence, recorded after a successful Core
division or real terminal death. It neither draws RNG nor changes the ledger.
Streaming carries the same channel across active-world rebuilds. Archive catch-up
has its own channel; save files do not serialize VFX. Loading is initialization,
not a birth/death event. Stale notifications are skipped and sequence overflow
drops old decoration, never biology. Render pause uses model time exclusively.

Death snapshots outlive biological corpse compaction without retaining Unity
objects. A local clip opens the front shell where the gel jet originates.
Exhaustion is subdued and slower; consumed/recently damaged cells open sooner.
There is no separate age-death mechanism in Core, so no invented natural-decay
cause was added. Decorative blue gel and purple remnants are instanced drawing
only: no FoodCloud, inventory, food hint, mass or resource is created by VFX.
Core's existing carcass/recycling accounting remains the sole resource source.

## Executed validation

| Gate | Result |
|---|---|
| Clean base .NET NUnit | 256 passed, 0 failed, 0 skipped; 4m14s |
| Full modified .NET NUnit | 269 passed, 0 failed, 0 skipped; 3m45s |
| New targeted tests after final pose polish | 13 passed, 0 failed, 0 skipped |
| C# 9 syntax, all Assets C# | 92 files, 0 syntax errors |
| Separate Core/Presentation/Volume/Localization/test assemblies, C# 9 | PASS; no Unity stubs |
| Existing colony, v1-v5, chunk, compaction, ledger, four-seed 600s ecosystem and starvation tests | Included in the full passing suite |
| Localization / audio module / asmdef JSON / source .meta / unique GUIDs / diff-check | PASS; 135 unique GUIDs |
| Unity import, Runtime API and shader compilation | NOT RUN |
| Unity PlayMode, including two new render regressions | NOT RUN |
| Real screenshots, GIF/video, artistic acceptance and Unity CPU/GPU allocations | NOT RUN |

Exact TRX-derived counts and 13 named test results:
[`diagnostics/issue-32-tests-summary.json`](diagnostics/issue-32-tests-summary.json).
The new tests cover paid-event identity, refusal, single death notification,
cause projection, no VFX resource changes, journal overflow, streaming continuity,
no historical replay on restoration, bounded deformation and the unchanged Core
at 15/60/144 presentation samples. Sampling does not assert visual equality
between Unity frame rates. Existing colony tests verify that parent death does
not kill the surviving child and that v5 topology and roles round-trip.

Commands:

```sh
dotnet test Tools/CoreTests/CoreTests.csproj -c Release --nologo
dotnet test Tools/CoreTests/CoreTests.csproj -c Release --filter FullyQualifiedName~LivingWaterTests
python3 Tools/check_model_assemblies.py
python3 Tools/check_localization.py
dotnet run --project Tools/LivingWaterBenchmarks/LivingWaterBenchmarks.csproj -c Release
git diff --check
```

## Budgets and measurements

Hard limits: 13 detailed residents (selected + 12), low-poly distant shells at
14–36 units, 128 instances per body/membrane submission, 256 per particle
submission, 256 journal entries, 32 temporary fission envelopes, 32 death tails,
24 gel slots and at most 6 membrane bubbles per tail. All meshes/materials are
shared and explicitly disposed. Temporary effects use fixed arrays and instanced
slots, with no per-particle GameObjects, coroutines or ParticleSystems.

Expanded mesh bounds cover animated outlines; picking uses the corresponding
deformed vertices and smoothed orientation. LOD changes do not reset the active
animation state. Offscreen/hidden organisms release cached presentation states.
Save version remains v5; no serialized field or gene is added.

The CPU **parameter projection** target was <1 ms for 512 residents and zero
steady-state managed allocation in that isolated loop. Results, with warm-up,
1,000 iterations and 13 detailed projections, are in
[`diagnostics/issue-32-presentation-cpu.csv`](diagnostics/issue-32-presentation-cpu.csv).
They measure .NET pose/curve arithmetic only, not Unity draw preparation, cilia
geometry, journal initialization, streaming, rendering, GPU or FPS. Unity frame
budgets remain unmeasured; no FPS is claimed. Simultaneous-event overflow may
omit some decoration in a crowded scene. This is a declared quality cap.

## Prepared real-Unity capture route — authorization still required

The ordinary preview route stays unchanged. The additional flag below opts into
this stage's route using the **same renderer and real existing residents**:

```sh
EVOLUTION_PREVIEW_SHA=<exact-40-character-head> ./Evolution.x86_64 \
  -evolution-visual-preview <output-directory> -evolution-living-cells
python3 Tools/ci/check_division_capture.py <output-directory>
python3 Tools/ci/check_living_capture.py <output-directory>
```

Run with an actual graphics context, not `-nographics`. No fixture residents,
position changes, genome edits, food grants or energy injection are used. Ordinary
movement commands exercise three naturally present inherited forms; a resident
earns a paid division; the route then waits for an actual nearby death. If a
required resident or event is unavailable, capture fails rather than fabricating
one. It uses an in-memory archive and does not read/write the player's saves.

Expected original output: 40 PNGs each for amoeboid/ciliary/flagellar movement,
49 fission PNGs plus the before frame, 81 death PNGs through dissolution,
`division.json` with paid birth/inheritance evidence and `living-cells.json` with
Unity version, exact revision, UTC capture date, identities, genes, ticks and
death sequence/cause. No such output exists for this change yet. Human review
must inspect normal-scale and macro views, direction changes, full neck break,
retained bridges, visible gel leakage, pause and colony survival. Metadata/image
validators are evidence checks, not a substitute for watching the animation.

The existing GitHub workflows and their opt-in protections were not changed.
The new additional capture flag is a manual player invocation; merely adding
the existing `visual-preview` label does not select this new route. Unity checks
and capture must be agreed separately before execution.

## Independent review focus

1. Verify no resource/PRNG/state mutation exists in presentation and journal reads.
2. Inspect journal continuity, stale-sequence handling and no-replay restoration.
3. Inspect true IDs and retained-bond handling through both lifecycle transitions.
4. Compile shaders/Runtime and run PlayMode in Unity 6000.6.9f1.
5. After authorization, inspect original Unity sequences and measure actual frame
   CPU/GPU/allocations with hundreds of residents. Keep this PR Draft until then.
