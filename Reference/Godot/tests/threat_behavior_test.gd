extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 83)
	var prey: Dictionary = sim.organisms[0]
	prey.genes.predation = 0.0
	prey.genes.herbivory = 0.0
	prey.genes.gills = 1.0
	prey.genes.temperature_tolerance = 2.0
	prey.pos = sim.sim_bounds.get_center()
	var hunter_genes: Dictionary = prey.genes.duplicate(true)
	hunter_genes.predation = 0.95
	var hunter := sim.make_organism(prey.pos + Vector2(20, 0), hunter_genes, 0, -1)
	sim.organisms.append(hunter)
	sim.step(0.2)
	assert(prey.behavior == "FLEEING", "A non-hunting microbe must detect and flee a nearby predator")
	assert(prey.threat_snapshot.id == hunter.id)
	assert(not prey.threat_snapshot.has("genes"), "Threat cache must store a snapshot, not organism references")
	var distant := sim.sim_bounds.position + Vector2(2, 2)
	hunter.pos = distant
	prey.threat_timer = 0.0
	sim._organism_grid_timer = 0.0
	sim.step(0.2)
	assert(prey.behavior != "FLEEING", "Expired threat must be cleared when a predator leaves")
	print("Threat behavior tests passed: passive microbe escape, snapshot cache, threat expiry")
	quit(0)
