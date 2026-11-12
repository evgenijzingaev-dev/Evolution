extends SceneTree

const Simulation = preload("res://scripts/simulation_manager.gd")
const PlanetData = preload("res://scripts/planet_manager.gd")
const Biomes = preload("res://scripts/biome_manager.gd")
const Save = preload("res://scripts/save_manager.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim = Simulation.new()
	_expect(sim.organisms.size() == Simulation.STARTING_POPULATION, "starting population seeded")
	_expect(sim.food.size() == Simulation.FOOD_TARGET, "nutrient field seeded")
	var seeded_a = Simulation.new()
	seeded_a.seed_world(1, 4, 12345)
	var seeded_b = Simulation.new()
	seeded_b.seed_world(1, 4, 12345)
	_expect(seeded_a.organisms.size() == 1 and seeded_a.planet_index == 4, "single-cell experiment starts on the selected planet")
	_expect(seeded_a.organisms[0].pos == seeded_b.organisms[0].pos and seeded_a.organisms[0].genes == seeded_b.organisms[0].genes, "experiment seed reproduces its initial world")
	seeded_b.seed_world(1, 0, 12345, 11)
	var landing_position: Vector2 = seeded_b.organisms[0].pos
	_expect(landing_position.x > seeded_b.sim_bounds.position.x + seeded_b.sim_bounds.size.x * 0.75 and landing_position.y > seeded_b.sim_bounds.position.y + seeded_b.sim_bounds.size.y * 0.66, "founder lands in the selected sector")
	_expect(PlanetData.PLANETS.size() == 5, "five planet profiles available")
	var west_biome := Biomes.at_position(Vector2(320, 136), sim.sim_bounds, 0)
	var east_biome := Biomes.at_position(Vector2(1115, 770), sim.sim_bounds, 0)
	_expect(west_biome.name != east_biome.name, "biome grid has local environmental differences")
	for o in sim.organisms:
		_expect(o.genes.has("temperature_tolerance") and o.genes.has("radiation_tolerance") and o.genes.has("phototrophy") and o.genes.has("predation"), "environmental and trophic genes initialized")
		break
	var test_genes: Dictionary = GeneticsSystem.starter(sim.rng)
	test_genes.predation = 0.9
	_expect(GeneticsSystem.organism_kind(test_genes) == "Predator", "predator phenotype follows genes")
	test_genes.predation = 0.0
	test_genes.decomposition = 0.9
	_expect(GeneticsSystem.organism_kind(test_genes) == "Decomposer", "decomposer phenotype follows genes")
	for i in range(1800):
		sim.step(Simulation.TICK_SECONDS)
	_expect(sim.elapsed > 300.0, "fixed simulation steps advance time")
	_expect(sim.births > 0, "organisms reproduce")
	_expect(sim.mutations > 0, "offspring inherit mutations")
	_expect(sim.deaths > 0, "energy and age cause mortality")
	_expect(sim.discovered_species.size() > 0, "species records are created")
	_expect(not sim.statistics_history.is_empty(), "population statistics are sampled")
	_expect(sim.ecosystem_stability >= 0.0 and sim.ecosystem_stability <= 1.0, "ecosystem stability stays within bounds")
	sim.select_planet(4)
	_expect(PlanetData.get_planet(sim.planet_index).name == "Thalassa", "planet selection changes conditions")
	var saved_target_food := int(sim.organisms[0].target_food)
	var saved_behavior := str(sim.organisms[0].behavior)
	_expect(Save.save_simulation(sim) == OK, "save file is written")
	var restored = Simulation.new()
	_expect(Save.load_simulation(restored) == OK, "save file loads")
	_expect(restored.organisms.size() == sim.organisms.size(), "population survives save and load")
	_expect(is_equal_approx(restored.elapsed, sim.elapsed), "simulation clock survives save and load")
	_expect(restored.planet_index == sim.planet_index, "selected planet survives save and load")
	_expect(restored.statistics_history.size() == sim.statistics_history.size(), "statistics survive save and load")
	_expect(restored.organisms[0].has("kind") and restored.organisms[0].has("behavior"), "organism traits and behavior survive save and load")
	_expect(restored.organisms[0].target_food == saved_target_food and restored.organisms[0].behavior == saved_behavior, "organism behavior state survives save and load")
	restored._step_active = true
	_expect(Save.load_simulation(restored) == ERR_BUSY, "load refuses to interrupt an active simulation step")
	restored._step_active = false
	var main_scene: Node = load("res://scenes/main.tscn").instantiate()
	root.add_child(main_scene)
	await process_frame
	main_scene.call("_show_statistics")
	await process_frame
	_close_child_windows(main_scene)
	await process_frame
	main_scene.call("_show_history")
	await process_frame
	_close_child_windows(main_scene)
	await process_frame
	main_scene.call("_show_lineage")
	await process_frame
	_expect(main_scene.get_child_count() >= 2, "statistics, history and lineage windows open")
	main_scene.call("_show_experiment_setup")
	var picker: OptionButton = main_scene.get("planet_picker")
	picker.select(2)
	var founder_slider: HSlider = main_scene.get("founder_slider")
	founder_slider.value = 1
	var seed_input: LineEdit = main_scene.get("seed_input")
	seed_input.text = "5678"
	main_scene.call("_begin_experiment")
	var ui_sim = main_scene.get("sim")
	_expect(ui_sim.organisms.size() == 1 and ui_sim.planet_index == 2 and not ui_sim.paused, "experiment setup launches a one-cell ecosystem on the chosen planet")
	var original_id: int = ui_sim.organisms[0].id
	main_scene.call("_show_experiment_setup")
	var pause_key := InputEventKey.new()
	pause_key.pressed = true
	pause_key.keycode = KEY_SPACE
	main_scene.call("_input", pause_key)
	_expect(ui_sim.paused, "keyboard shortcuts cannot resume the world behind setup")
	seed_input.text = "invalid"
	main_scene.call("_begin_experiment")
	_expect(ui_sim.organisms[0].id == original_id and ui_sim.paused, "invalid seed preserves the existing experiment")
	main_scene.call("_return_to_experiment")
	_expect(not ui_sim.paused and ui_sim.organisms[0].id == original_id, "return restores the previous experiment and pause state")
	main_scene.queue_free()
	print("Smoke tests passed: population=%d births=%d deaths=%d mutations=%d species=%d biomes=%d" % [sim.organisms.size(), sim.births, sim.deaths, sim.mutations, sim.discovered_species.size(), Biomes.biomes_for_planet(sim.planet_index).size()])
	quit(0)

func _expect(condition: bool, label: String) -> void:
	if not condition:
		push_error("Smoke test failed: " + label)
		quit(1)

func _close_child_windows(parent: Node) -> void:
	for child in parent.get_children():
		if child is Window:
			child.hide()
			child.queue_free()
