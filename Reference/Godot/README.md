# EVOLUTION: LIFE BEYOND — prototype

An asset-free Godot 4.x prototype of an evolving microbial population in a pelagic environment.

## Run

Для запуска без редактора дважды нажмите **Запустить игру.cmd**. Русская памятка: **КАК ИГРАТЬ.md**. Движок из папки Godot должен оставаться рядом с проектом.

Open this folder in Godot 4.x and press **F6/F5** (or run `godot --path .`). The project uses only built-in Godot nodes and drawing APIs.

Run the headless smoke suite with `Godot/Godot_v4.7.2-stable_win64_console.exe --headless --path . --script res://tests/simulation_smoke.gd`.

Measure simulation throughput and incremental frame chunks with `Godot/Godot_v4.7.2-stable_win64_console.exe --headless --path . --script res://tests/performance_bench.gd`.

Run the full regression suite in PowerShell with `./tools/run_tests.ps1`. It checks script/assertion output as well as process exit codes and writes `tests/results/suite.log`. Mixed-role performance and balance reports are separate scripts under `tests/`.

`tools/visual_review.gd` runs with the graphical Godot driver, loads the recorded rich-population snapshot and saves real rendered review images under `tests/results/review_*.png`, then exits. It requires the long-term report's seed-202 snapshot. This graphical capture check is separate from the headless regression suite.

## Controls

- **Space** or **Pause**: pause/resume.
- **Speed**: cycle through ×1, ×2, ×5, ×10 and **МАКС**. Maximum mode runs multiple unchanged fixed steps while there is time left in the frame's soft 8 ms work budget, up to 64 completed steps per call. Large grid/commit operations can exceed that budget; actual acceleration depends on population and hardware. Pause and save/load remain available.
- **NEW EXP**: configure a new experiment, or return to the current one.
- **SETTINGS**: change mutation chance/strength, nutrient recovery and the birth ceiling. Settings persist in saves; reducing the ceiling does not remove existing organisms.
- Organisms have health, inherited energy capacity, reproduction rate, resilience, flagella and fins. Health recovers with sufficient energy; defenses reduce damage and severe environmental stress can injure organisms. Luminescence improves sensing in darkness at an energy cost.
- Offspring reserves are transferred from the parent (or funded equally by a mating pair), limited by parental reserves and the child's inherited capacity. Birth consumes additional energy, increasing with the child's tissue count. Founder initialization remains separate from reproduction; division no longer creates a free stored-energy bonus.
- Fins and flagellar tails are drawn from the genome. Passive microbes detect nearby predators through a dedicated spatial index and flee; selecting an organism reveals its health bar.
- A sexual reproduction gene can evolve from primitive asexual founders. Mature sexual organisms seek compatible partners, require contact and energy from both, then produce a recombined and potentially mutated genome. Both parent IDs and mating cooldowns are saved.
- Coloniality and differentiation produce bodies of up to nine cells. Feeding, motor and defense tissues affect energy acquisition, movement and survival, with upkeep and reproductive costs. Tissue composition and colors come from the inherited genome and can be inspected in the genome/species windows.
- Humidity, water, atmospheric toxicity and gravity affect survival or movement. The environment button opens a Russian event picker; climate events can change oxygen, humidity, water and light as well as temperature, radiation and food recovery. Active effects persist in saves.
- Local refugia have distinct humidity/water and shielding: an oasis differs from open desert, and a caldera lake differs from lava terrain. The landing map displays these local conditions. Statistics include recorded deaths from starvation, age, environmental damage and predation.
- **Click a cell**: inspect its phenotype, current behavior, genes, generation and energy.
- **Mouse wheel / middle-drag**: zoom and pan the observation chamber.
- **+**: advance the speed setting.
- Right panel: open statistics, event history and species tree.
- History includes all recorded events, newest first, with translated text search, category filters, 100-row pages and event details. **Обновить** refreshes the history snapshot from the current experiment.
- **TREE**: select parent/child branches to inspect discovery, extinction, population and mutations recorded at branch origin. **FULL GENOME**: inspect every gene of the selected organism. Both include Russian explanations of expressed adaptations and tradeoffs; sample nutrient/organic yields are calculated by the feeding system. Inspection windows show stable snapshots. **Обновить дерево** reloads current species/counts while preserving the selected branch and collapsed nodes; **Обновить выбранный организм** reloads the organism currently selected on the map.
- Left panel: cycle planets and trigger a climate event.
- Footer: save and load experiments.
- Automatic saves use a separate slot from manual saves. Both slots can be loaded from experiment setup. Writes use a temporary file and rename; malformed save data is checked before replacing the current simulation.
- Save v8 preserves finite organic resource reserves and resource classes alongside random generators, event schedules, pause/time state and runtime caches. Paired tests check matching continuation on the same Godot build, with a tight floating-point tolerance for organic reserves. Older saves remain loadable with defaults for newly added fields; unclassified particles default to mineral nutrients, and missing resource reserves receive a deterministic value of 8.
- Hunting uses periodic sampled spatial searches and saved target IDs. Large populations use an 8 ms organism-processing budget per frame; spatial indexing and commit work can add time beyond that budget. The mixed-role 10k benchmark still runs below real-time simulation throughput.
- Food choice weighs distance and dietary preference against the destination biome's physiological stress for that genome. This reduces repeated conflict between foraging into hostile terrain and migration back into a refuge. Resource-biome lookup is shared; stress comparisons run during periodic food searches, not on every movement update.
- Empty or depleted food targets respect a staggered 1.1–1.9 simulated-second search interval. Invalid targets are cleared immediately; founders/newborns still search on their first update. This bounds repeated work during dense competition, with a short reaction delay when new food appears.
- Biomes use cached subtle procedural substrate textures with their existing sector colors; cell nuclei add detail when zoomed in. These visual layers use their own deterministic noise and do not consume simulation RNG or change habitat conditions.
- Migration reuses identical biome stress calculations within each decision without changing neighbor/tie order. The frame budget includes preparation time; populations over 1000 receive a separate commit work slice, reducing stacked frame spikes in normal and maximum modes. Individual grid/commit operations can still exceed the soft budget.

At startup, the experiment setup screen lets you choose one of the five planets, a landing sector and its biome, the number of founding cells, and an optional reproducible random seed. You can also load a saved experiment. **NEW EXP** opens the same setup again; Return or Escape restores the current experiment and its previous pause state. Autosaving begins after an experiment has been started.

The game interface is Russian. **ГАЛАКТИКА** opens a clickable view of the five planets. **ОТКРЫТЬ КАРТУ БИОМОВ** opens the landing-sector map with local conditions. Click or use arrow keys and confirm the choice; closing the window cancels selection. A new world begins only when you launch the configured experiment.

**УСЛОВИЯ ПЛАНЕТЫ** in the HUD or **КЛИМАТ** in setup opens the planetary editor. Each world has independent saved overrides for climate, pressure, radiation, atmosphere-related values, gravity, water, light, geology and productivity. Edits preserve organisms and affect selection; a reset button restores defaults. These templates also apply when starting a new experiment on that world.

Manual saves create separate snapshots in the experiment archive. **ЗАГРУЗИТЬ** or **АРХИВ ЭКСПЕРИМЕНТОВ** opens historical snapshots and quick slots with date/planet/population information. Save/load requests automatically wait for a complete simulation step, including a paused partial step. Autosaves use their separate slot.

## Prototype systems

- `scripts/genetics_system.gd`: heritable traits, mutation and a coarse phenotype signature used to identify species.
- `scripts/behavior_system.gd`, `scripts/ecosystem_system.gd`, `scripts/evolution_system.gd` and `scripts/organism_manager.gd`: reusable behavior, spatial nutrient grid, species-history and organism-creation logic.
- `scripts/simulation_manager.gd`: fixed-step movement, local spatial searches, consumption, metabolism, reproduction, selection, aging, death, discovered branches and extinction tracking. Large populations update in bounded chunks so each frame can return to the UI.
- `scripts/planet_manager.gd`: Terra, Arrox, Nivea, Ignis and Thalassa with distinct climate, pressure, radiation, water and food productivity.
- `scripts/biome_manager.gd`: spatially distinct biomes on every planet; movement into a biome changes the local conditions, and stressed cells migrate toward safer neighboring zones.
- `scripts/environment_system.gd`: climate events with durations and persistent experiment history.
- `scripts/save_manager.gd`: versioned JSON save/load, plus automatic saves every two minutes to the Godot user data folder. Saves preserve organism behavior timers and migration state; loading is blocked while a simulation step is in progress.
- `scripts/statistics_view.gd`: population and species history chart.
- `scripts/world_view.gd`: clipped map rendering, selection, zoom and panning.
- `tests/simulation_smoke.gd` and `tests/performance_bench.gd`: simulation, persistence, UI-window and throughput checks.
- `scripts/main.gd`: procedural organism and water rendering, scientific HUD, time and inspection controls.
- `scenes/main.tscn`: project entry scene.

Nutrients are finite between slow environmental replenishment events. Gold particles are mineral nutrients; brown particles are organic remains generated by deaths. Producers use light or geology for energy; grazers feed on producer cells, including colonial producers; predators hunt smaller organisms. Omnivores inherit both grazing and predatory capacities. Scavenging and decomposition genes improve organic energy yield and preference, while strong dietary specialization reduces mineral-food yield. Food searches retain both resource classes in dense spatial buckets. Statistics distinguish scavengers and omnivores and count dietary roles independently of colonial body shape. Mutations change feeding strategies, defenses, sensory organs, appendages and environmental tolerances. Speed, sensing and feeding efficiency affect behavior; metabolism, size, climate, radiation, pressure and oxygen affect energy use and survival; lifespan affects mortality. Each planet has a tiled biome map. Environmental events occur over time or can be triggered manually. The event log, species tree and statistics graph are available on the right panel. The displayed geological clock is a prototype scale, not literal biological years.

Organic remains now have finite reserves derived from 75% of the victim's remaining energy and funded body formation cost. Immediate predation intake is deducted from that same reserve before a carcass is created. Organic assimilation is capped at 90% even for highly efficient specialized tissues. Closed-system tests account for living reserves, funded body structure and remaining carcasses; repeated recycling cannot create energy. These are abstract gameplay quantities, not a complete thermodynamic model of mineral chemistry or photosynthesis.

## Scope

The playable prototype includes the core evolution loop, five planets, galaxy and landing screens, experiment setup/archive, editable planetary conditions, biome maps, trophic roles, environmental events, saves, statistics, organism inspection, lineage and camera controls. It is not a finished release: harsh-world balance, longer ecosystem validation, visual QA across window sizes and further 10,000-organism tuning remain open. The latest 600-second single-founder refugia report retains all three Terra lineages (45/55/30 organisms); four sampled harsh-world lineages also reach this horizon (one each on Arrox/Nivea, two on Ignis), while all three Thalassa cases go extinct. Earlier one-hour Terra runs with finite organic reserves survived at 184/1288/187 organisms, before the food-search frequency adjustment; their reports and loadable final snapshots are under tests/results. The latest mixed-role 10k benchmark averages about 398 ms per fixed step with a worst work call about 23 ms, still below real-time throughput. These headless timings do not measure graphical frame rate. The bundled-engine launcher is available; a separately exported distribution has not been built.
