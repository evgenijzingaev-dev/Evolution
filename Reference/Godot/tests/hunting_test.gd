extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 91)
	var prey: Dictionary = sim.organisms[0]
	prey.pos = sim.sim_bounds.get_center()
	prey.genes.speed = 0.0
	prey.genes.predation = 0.0
	prey.health = 1.0
	var genes: Dictionary = prey.genes.duplicate(true)
	genes.predation = 1.0
	var predator := sim.make_organism(prey.pos, genes, 0, -1)
	predator.energy = 18.0
	predator.hunt_timer = 0.0
	sim.organisms.append(predator)
	sim.step(0.2)
	assert(prey.dead and sim.deaths == 1, "A sampled nearby prey must be attacked")
	assert(predator.target_prey == prey.id and predator.hunt_timer > 0.0)
	sim.build_organism_grid()
	assert(sim._organisms_by_id.has(predator.id), "Spatial-grid rebuilding must preserve the current ID lookup")
	sim.step(0.2)
	assert(sim.deaths == 1, "Cached dead prey cannot be killed twice")
	assert(predator.behavior != "HUNTING", "Cached targets must be checked for liveness")
	print("Hunting tests passed: sampled acquisition, attack, timer cache and dead-target invalidation")
	quit(0)
