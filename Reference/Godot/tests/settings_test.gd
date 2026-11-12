extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 17)
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	for i in range(50):
		assert(GeneticsSystem.inherit(genes, sim.rng, 0.0, 0.5) == genes, "Zero mutation chance must preserve genes")
		assert(GeneticsSystem.inherit(genes, sim.rng, 1.0, 0.0) == genes, "Zero strength must preserve genes")
	var mutated := GeneticsSystem.inherit(genes, sim.rng, 1.0, 0.3)
	var mean_relative_change := 0.0
	for index in range(1000):
		var sample := GeneticsSystem.inherit({"temperature_tolerance":1.0}, sim.rng, 1.0, 0.12)
		mean_relative_change += absf(float(sample.temperature_tolerance) - 1.0) / 1000.0
	assert(mean_relative_change > 0.06 and mean_relative_change < 0.14, "Configured scalar mutation strength must not be silently reduced by an extra factor")
	assert(mutated != genes and sim.organisms[0].genes == genes, "Mutation must change only the offspring genome")
	sim.configure_settings({"mutation_rate":0.0,"population_limit":1,"resource_regrowth":0.0})
	for i in range(100):
		for organism in sim.organisms:
			organism.energy = 30.0
		sim.step(0.2)
	assert(sim.births == 0, "Population ceiling must block new births")
	const Save = preload("res://scripts/save_manager.gd")
	const PATH := "user://settings_test.json"
	assert(Save.save_simulation(sim, PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, PATH) == OK and restored.settings == sim.settings, "Settings must survive save/load")
	var window := preload("res://scripts/ui_manager.gd").show_settings(root, restored)
	assert(window.get_child_count() == 1)
	window.queue_free()
	DirAccess.remove_absolute(PATH)
	print("Settings tests passed: mutation controls, independent genomes, birth ceiling, persistence, settings window")
	quit(0)
