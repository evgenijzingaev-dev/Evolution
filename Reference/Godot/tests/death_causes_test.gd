extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 55)
	var organism: Dictionary = sim.organisms[0]
	organism.energy = -1.0
	sim.step(0.2)
	assert(sim.deaths == 1 and sim.death_causes.get("starvation", 0) == 1)
	sim._kill_organism(organism, "age")
	assert(sim.deaths == 1 and sim.death_causes.size() == 1, "A death must be recorded exactly once")
	const Save = preload("res://scripts/save_manager.gd")
	const PATH := "user://death_causes_test.json"
	assert(Save.save_simulation(sim, PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, PATH) == OK and restored.death_causes == sim.death_causes)
	DirAccess.remove_absolute(PATH)
	print("Death cause tests passed: attribution, single accounting and saved diagnostics")
	quit(0)
