extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 13)
	sim.configure_settings({"mutation_rate":0.0})
	var first: Dictionary = sim.organisms[0]
	first.genes.sexual_reproduction = 1.0
	first.genes.reproduction_rate = 1000.0
	first.genes.speed = 0.0
	first.genes.gills = 1.0
	first.genes.temperature_tolerance = 2.0
	first.energy = 30.0
	first.age = 20.0
	first.pos = sim.sim_bounds.get_center()
	sim.step(0.2)
	assert(sim.births == 0 and first.behavior == "SEEKING_MATE", "Sexual organisms must search for a partner, not divide alone")
	var other_genes: Dictionary = first.genes.duplicate(true)
	other_genes.fins = 1.0
	var second := sim.make_organism(first.pos + Vector2(5, 0), other_genes, 2, -1)
	second.species = first.species
	second.energy = 30.0
	second.age = 20.0
	sim.organisms.append(second)
	sim._organism_grid_timer = 0.0
	sim.step(0.2)
	assert(sim.births == 1, "A nearby compatible pair must create exactly one offspring")
	var child: Dictionary = sim.organisms.back()
	assert(child.parent == first.id and child.second_parent == second.id and child.generation == 3)
	assert(first.reproduction_cooldown > 0.0 and second.reproduction_cooldown > 0.0)
	assert(sim.mutations == 0, "Recombination alone must not count as mutation")
	assert(child.genes.fins == first.genes.fins or child.genes.fins == second.genes.fins)
	const Save = preload("res://scripts/save_manager.gd")
	const PATH := "user://reproduction_test.json"
	assert(Save.save_simulation(sim, PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, PATH) == OK)
	assert(restored.organisms.back().second_parent == second.id and restored.organisms[0].reproduction_cooldown > 0.0)
	DirAccess.remove_absolute(PATH)
	print("Reproduction tests passed: mate search, compatible pair, two parents, cooldown, recombination, persistence")
	quit(0)
