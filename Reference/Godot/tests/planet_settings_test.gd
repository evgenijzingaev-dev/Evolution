extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 3, 36)
	var original := sim.planet_data()
	var id: int = sim.organisms[0].id
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	var biome := BiomeManager.at_position(sim.organisms[0].pos, sim.sim_bounds, 3)
	var before: float = sim._environmental_stress(genes, original, biome, sim.environment.event_data())
	sim.configure_planet(3, {"temperature":18.0,"radiation":0.0,"pressure":1.0,"oxygen":8.0,"toxicity":0.0,"water":1.0,"humidity":1.0})
	var after: float = sim._environmental_stress(genes, sim.planet_data(), biome, sim.environment.event_data())
	assert(after < before and sim.organisms[0].id == id and sim.organisms[0].genes == genes, "Climate controls must affect selection without editing organisms")
	assert(sim.planet_data(0).temperature == PlanetManager.get_planet(0).temperature, "Changes must remain specific to one planet")
	sim.step(0.2)
	assert(sim._step_planet.temperature == 18.0 and not sim._environment_dirty)
	const Save = preload("res://scripts/save_manager.gd")
	const PATH := "user://planet_settings_test.json"
	assert(Save.save_simulation(sim, PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, PATH) == OK and restored.planet_data().temperature == 18.0)
	restored.seed_world(1, 3, 36)
	assert(restored.planet_data().temperature == 18.0, "Configured world templates must survive starting a new experiment")
	var window := preload("res://scripts/ui_manager.gd").show_planet_settings(root, restored, 3)
	var temperature: SpinBox = window.find_child("temperature", true, false)
	temperature.value = 25.0
	window.find_child("ApplyPlanetConditions", true, false).emit_signal("pressed")
	assert(restored.planet_data().temperature == 25.0, "Editor apply must update the simulation")
	restored.reset_planet_conditions(3)
	assert(restored.planet_data() == original, "Reset must recover the original world profile")
	DirAccess.remove_absolute(PATH)
	await process_frame
	print("Planet settings tests passed: selection effects, preserved genomes, per-world values, persistence, editor apply and reset")
	quit(0)
