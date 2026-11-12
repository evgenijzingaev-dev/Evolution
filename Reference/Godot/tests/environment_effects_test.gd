extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 67)
	var planet := PlanetManager.get_planet(0)
	var biome: Dictionary = BiomeManager.biomes_for_planet(0)[0]
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	var wet := planet.duplicate(true)
	wet.humidity = 1.0
	wet.water = 1.0
	var dry := planet.duplicate(true)
	dry.humidity = 0.0
	dry.water = 0.0
	var event := sim.environment.event_data()
	var arrox := PlanetManager.get_planet(1)
	var arrox_biomes := BiomeManager.biomes_for_planet(1)
	assert(sim._environmental_stress(genes, arrox, arrox_biomes[2], event) < sim._environmental_stress(genes, arrox, arrox_biomes[1], event), "An oasis must provide genuinely different local conditions")
	var high_gravity := SimulationManager.new()
	high_gravity.seed_world(1, 0, 123)
	var low_gravity := SimulationManager.new()
	low_gravity.seed_world(1, 0, 123)
	var heavy := planet.duplicate(true)
	heavy.gravity = 4.0
	heavy.water = 0.0
	var light := heavy.duplicate(true)
	light.gravity = 0.5
	var origin: Vector2 = high_gravity.organisms[0].pos
	high_gravity._process_organism(high_gravity.organisms[0], 0.2, heavy, event)
	low_gravity._process_organism(low_gravity.organisms[0], 0.2, light, event)
	assert(origin.distance_to(high_gravity.organisms[0].pos) < origin.distance_to(low_gravity.organisms[0].pos), "Gravity must affect actual movement")
	assert(sim._environmental_stress(genes, dry, biome, event) > sim._environmental_stress(genes, wet, biome, event), "Dry environments must increase stress")
	var armored := genes.duplicate(true)
	armored.armor = 1.0
	assert(sim._environmental_stress(armored, dry, biome, event) < sim._environmental_stress(genes, dry, biome, event), "Protective cover must reduce dehydration stress")
	var toxic := planet.duplicate(true)
	toxic.toxicity = 0.8
	var resilient := genes.duplicate(true)
	resilient.resilience = 1.0
	assert(sim._environmental_stress(resilient, toxic, biome, event) < sim._environmental_stress(genes, toxic, biome, event))
	sim.trigger_environment_event("Drought")
	assert(sim.environment.current_event == "Drought" and sim.environment.humidity_multiplier < 1.0 and sim.environment.water_shift < 0.0)
	sim.trigger_environment_event("Atmospheric change")
	const Save = preload("res://scripts/save_manager.gd")
	const PATH := "user://environment_test.json"
	assert(Save.save_simulation(sim, PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, PATH) == OK)
	var restored_effects := restored.environment.event_data()
	for key in sim.environment.event_data():
		if key == "name":
			assert(restored_effects[key] == sim.environment.event_data()[key])
		else:
			assert(is_equal_approx(float(restored_effects[key]), float(sim.environment.event_data()[key])), "Active environmental effect must survive loading: " + str(key))
	sim.environment.step(1000.0)
	assert(sim.environment.current_event == "" and sim.environment.oxygen_multiplier == 1.0 and sim.environment.toxicity_shift == 0.0)
	var window := preload("res://scripts/ui_manager.gd").show_events(root, restored)
	assert(window.get_child(0).get_child(1).item_count == EnvironmentSystem.EVENTS.size() + 1)
	window.queue_free()
	DirAccess.remove_absolute(PATH)
	print("Environment effect tests passed: dehydration, armor, toxicity, controlled events, persistence, recovery, event picker")
	quit(0)
