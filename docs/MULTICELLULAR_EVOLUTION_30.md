# Issue #30 — connected multicellular life in WaterPrototype3D

Review candidate, not a claim of verified Unity gameplay. Issue: https://github.com/evgenijzingaev-dev/Evolution/issues/30 . Draft PR: https://github.com/evgenijzingaev-dev/Evolution/pull/31 . Base is PR #29, exactly `d8aea353736559cac641406311ee7ddb4c5309ab`, target `codex/controlled-cell-evolution`. No main merge, auto-merge, Player, Unity Editor, PlayMode, capture, paid API or game autopilot was invoked.

## Implementation

**A — real cells and paid connections.** Every daughter is a VolumeAgent with its own genome, energy, age, integrity, identity, parent and lineage. A retained birth costs the ordinary structure/reserve/overhead plus 0.30 dissipated attachment energy. Development remains independently paid. All checks operate on copies until birth succeeds. Preview checks genes, sockets, every member's size limit, offspring compatibility and physical room. A failed request has no attachment/development charge. NPC retention requires inherited adhesion above 0.72 and valid space/size; failed natural retention falls back to an independent birth. No prebuilt colonies are spawned.

Connections store the parent's permanent string identity, never an array index. The parent must be a live older birth ancestor from the same lineage. Invalid/missing/dead parents sever an edge, not the entire living subtree. Graphs cannot cycle through birth ancestry. Compaction reconstructs indices. New birth numeric IDs use a monotonic negative namespace from the persisted global creation order, separate from existing founder IDs; permanent identities remain authoritative.

Exchange visits each real edge once per biological update. The smaller Transport gene limits flux to `4 * Transport * dt`, additionally bounded by gradient, donor stock and receiver capacity. Receiver gets 95%; 5% is dissipated. Individual budgets record signed transfer; the persisted global ledger records contact, received transport and transport loss. Dead/exhausted cells cannot draw from neighbours before removal. All metabolism, growth, birth and feeding still use the actual Core physiology.

**B — expression and organism control.** Tissue slots and inherited RolePattern/Differentiation determine feeding, movement, defense and reproduction roles. Expression develops over time and fades after isolation; no genes are invented by a role selection UI. Feeding increases real Photosynthesize/Feed rates and reduces motor capability; movement increases motor capability with real swimming costs and lowers feeding; defense reduces actual bite damage and contributes bounded collective protection, while feeding/movement and maintenance pay the tradeoff. Reproduction changes maturation/division requirements through the existing Core functions.

Whole-organism motion uses summed motor capacity divided by total mass and size-dependent drag. Turning slows with mass/size. Every live cell pays its own propulsion and turning costs; members translate with the common velocity rather than being teleported to a parent's position. Single-cell control within a colony contributes to steering proportionally to its mass; whole mode commands all motors. Passive hard-sphere contacts remove overlap, obstacles constrain individual bodies, and excessive stretching breaks links. This is a simplified mechanical model, not a deformable hydrodynamics solver.

**C/D — UI, rendering, saving.** Open the existing development panel through “Организм / наследование”. It includes whole/single control, next-child separate/retain choice, detach selected branch, per-member control, roles, total reserve/capacity, individual energy/net/transport budgets and Russian refusal reasons. Y still follows a real direct offspring. R still requests paid division. Existing panel scaling, scrolling and input capture are reused; new controls live inside the modal panel. The compact organism card shows member count and selected role.

VolumePainter draws a connection only for a real live bond; intensity reflects measured transport. Role expression tints the membrane and changes motor flagella/defense structures. Shared colony phase coordinates animation. Bodies remain individually rendered and selectable; instanced batches and near/far detail are retained. These source changes have **not** been visually verified.

Save v5 records bond identity, birth connection choice, bond/colony numeric references, tissue slot, role, expression and last transfer, in addition to previous genomes/energy/plans. v1–v4 remain readable as unconnected organisms. The global ecology ledger upgrades from version 1 to 2. A connected component is assigned to the root's region, including members crossing a chunk edge, so streaming and isolated archive replay cannot split live links into different archives. After a real break/death, the resulting components obtain independent owners. Existing LastTick and owner cleanup prevent replaying influx twice or duplicating resident organisms. Whole-control mode survives streaming within the session; selected-player identity/control mode is not a new disk-persisted player profile.

## Validation barriers

- .NET Core/NUnit: **256 passed, 0 failed, 0 skipped** in the full final run (3m24s). Final results in `diagnostics/issue-30-tests-summary.json`; includes old regressions, 21 new cases, long seeded ecosystem tests and exact deterministic replay.
- C# 9: all 87 asset C# files parse; CoreTests build with `LangVersion=9.0`, zero warnings/errors. Separate Core/Volume/Presentation/Localization/Evolution.Tests compilation also passes. Added Volume internals access only for Evolution.Tests, fixing a boundary hidden by the monolithic test harness. This is not Unity API compilation.
- Static: localization checker, JSON/asmdef, required Unity audio dependency, .meta GUID uniqueness and diff whitespace checks pass.
- Unity 6000.6.9f1 import/API compilation, EditMode/PlayMode, actual 1366×768 / 1920×1080 / 2560×1440 hitboxes and visual frames: **NOT RUN / UNVERIFIED**. No rendering or gameplay acceptance is inferred from .NET or syntax checks. These remain separately authorized barriers.
- Model performance: `diagnostics/issue-30-model-budget.csv` samples 200 steps after warmup in a generated 481-agent world. Measurements are machine/load dependent and exclude Unity rendering, input and GC/frame behavior. NPC refusal text is now formatted only after biological eligibility; measured allocations fell from ~53.4 KB to ~5.9 KB per step. Existing births/readouts still allocate; no zero-allocation-frame claim.

## Reproduction commands

```sh
dotnet test Tools/CoreTests/CoreTests.csproj -c Release
dotnet build Tools/CoreTests/CoreTests.csproj -c Release -p:LangVersion=9.0
dotnet run --project Tools/VolumeDiagnostics -c Release -- --seconds=600
dotnet run --project Tools/VolumeDiagnostics -c Release -- --seconds=600 --no-supply
dotnet run --project Tools/ColonyDiagnostics -c Release
dotnet run --project Tools/ColonyDiagnostics -c Release -- --profile
python Tools/check_model_assemblies.py
python Tools/check_localization.py
git diff --check
```

The diagnostics program issues ordinary model commands in an isolated console test. It does not start Unity, control the user's computer or enable a scheduled game autopilot.

## Seeded population and conservation results

Fixed camera, 250 initial cells in known regions; 600 simulated seconds per seed. “Alive” includes active and archived cells; births and deaths are cumulative; generation is the maximum among survivors. Baseline is the checked-in PR #28 ecosystem CSV used by PR #29's unchanged natural behavior, not a newly claimed baseline execution. Connection/role/motion/contact changes alter competition, so exact old population counts are not expected. Supply rates are unchanged.

| Seed | Baseline alive | New active | New archived | New total | Births | Deaths | Generation | Ledger residual |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 42 | 235 | 151 | 89 | 240 | 1952 | 1962 | 12 | 0.493116 |
| 7 | 243 | 195 | 0 | 195 | 1774 | 1829 | 10 | 0.459070 |
| 123 | 191 | 174 | 0 | 174 | 1672 | 1748 | 7 | 0.462156 |
| 2026 | 208 | 193 | 0 | 193 | 1666 | 1723 | 9 | 0.439885 |

All 244 sampled rows per scenario have finite/nonnegative stocks and satisfy `alive = 250 + births - deaths`. An independent repeat of all four 600-second runs was byte-identical after the allocation optimization. Maximum absolute energy residual with supply is 0.493116 units (float accumulation; baseline 0.484764). The long tests bound residual against throughput at 0.005%. Stored chemical/biomass energy + dissipation equals initial stock + nutrient input + net solar input within that tolerance.

| Seed | Singletons | Pairs | Colonies 3–4 | Colonies 5–8 | Colonies 9–12 |
|---|---:|---:|---:|---:|---:|
| 42 | 232 | 4 | 0 | 0 | 0 |
| 7 | 195 | 0 | 0 | 0 | 0 |
| 123 | 172 | 1 | 0 | 0 | 0 |
| 2026 | 184 | 3 | 1 | 0 | 0 |

These columns count organisms, not cells. Singletons remain competitive; seed 7 has no surviving colony at 600 s. Large colonies are possible within gene limits but none remain in these endpoint samples. The measurements do not establish that larger colonies are universally advantageous or that the ecosystem survives indefinitely.

With external nutrient supply disabled, all four populations are extinct by 600 s; sunlight and finite detritus recycling remain in the model. Maximum residual is 0.003206. Full 10-second rows: `issue-30-after.csv`, `issue-30-no-supply.csv`; endpoint JSON: `issue-30-summary.json`.

## Actual paid model route

The console route selects an existing generated resident with adhesion/branching/differentiation and a viable food patch. It never edits genes, energy, growth, food or positions. It requests paid births and moves toward existing clouds with normal inputs. All four seeds achieve ≥4 cells with at least two expressed roles, switch to whole control, detach a living part and keep the remainder alive for five seconds. This demonstrates deliberate loss by detachment; natural parent death is covered separately by a model regression fixture, not represented as a recorded Unity event.

| Seed | First 2 cells (s) | First 4 cells (s) | Role mask at 30 s | Remaining cells at 35 s | Remaining energy |
|---|---:|---:|---:|---:|---:|
| 42 | 2.95 | 15.45 | 30 | 3 | 6.0642 |
| 7 | 2.80 | 12.55 | 28 | 3 | 6.9704 |
| 123 | 3.05 | 10.20 | 22 | 3 | 41.8709 |
| 2026 | 2.50 | 17.15 | 22 | 3 | 28.3455 |

Role mask: feeding=2, movement=4, defense=8, reproduction=16. Contact/transport columns in the route CSV are **world-wide** cumulative counters, including NPCs, not the selected organism’s isolated cost. Per-birth attachment price is independently asserted at 0.30.

## Specialization scenarios

Same inherited base genes, one second of actual Photosynthesize/Feed, subtracting actual maintenance; test fixtures explicitly express the role at 0.8. These are controlled physiology measurements, not full ecosystem survival estimates.

| Environment | Generalist net energy | Feeding specialist net energy | Interpretation |
|---|---:|---:|---|
| Light=1, substrate=20, organic=0 | 2.647600 | 5.642401 | Higher real food/light intake |
| Light=0, substrate=0, organic=20 | 1.786800 | 3.955232 | Higher real organic uptake |
| No light or food | -0.632400 | -0.786400 | Tissue costs lose without resources |

Additional assertions cover lower bite damage for defense, higher motor output with reduced feeding, paid movement of every member, conservative redistribution along edges, and the inability of transfer to create energy.

## Independent review focus and limitations

1. Review `VolumeColonies`, `VolumeWorld`, `ChunkOcean`, and v5 wire layout first. Atomic file replacement is per region; no new crash-atomic multi-file transaction was introduced. Archive regions are still biologically isolated until loaded, as in the base implementation.
2. Collision correction is a simplified hard-sphere projection; colonies translate with a common velocity, not a full angular rigid-body solver. Obstacle deformation can break links. Model tests do not verify visual membrane continuity or collision appearance.
3. Endpoint colony prevalence is low. Costs/drag reduce birth turnover relative to baseline; all seeds retain ongoing births/deaths and generations, but there is no claim of broad optimal balance from four seeds.
4. Validate actual Unity material/API compatibility, animation at 1/2/4/8 real cells, UI scaling/hitboxes, map/pause/LOD and renderer allocations after separate authorization. Screenshots were not generated or fabricated.
5. Disk persistence covers biological state and pending birth choices; player-selection/death-notification profile remains session-local, as in PR #29.
