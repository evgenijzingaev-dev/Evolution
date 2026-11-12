extends SceneTree

func _initialize() -> void:
	for role in ["Grazer","Predator","Scavenger","Decomposer"]:
		var supplied := _experiment(role,true)
		var deprived := _experiment(role,false)
		for tick in range(350):
			supplied.step(0.2)
			deprived.step(0.2)
		assert(supplied.organisms.size() == 1, "A consumer with its food source must retain energy: " + role)
		assert(deprived.organisms.is_empty() and deprived.death_causes.get("starvation",0) == 1, "Removing the food source must cause starvation: " + role)
	print("Food dependency tests passed: grazers, predators, scavengers and decomposers survive with food and starve without it")
	quit(0)

func _experiment(role: String, supplied: bool) -> SimulationManager:
	var sim := SimulationManager.new()
	sim.seed_world(1,0,70)
	sim.settings.resource_regrowth = 0.0
	sim._next_event_time = 10000.0
	sim.food.clear()
	sim.resource_kinds.clear()
	sim.resource_reserves.clear()
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	genes.phototrophy = 0.0
	genes.chemotrophy = 0.0
	genes.predation = 1.0 if role == "Predator" else 0.0
	genes.herbivory = 1.0 if role == "Grazer" else 0.0
	genes.scavenging = 1.0 if role == "Scavenger" else 0.0
	genes.decomposition = 1.0 if role == "Decomposer" else 0.0
	genes.efficiency = 1.0
	genes.speed = 0.0
	genes.metabolism = 0.8
	genes.lifespan = 1000.0
	genes.reproduction_rate = 0.0
	genes.gills = 1.0
	genes.temperature_tolerance = 2.0
	genes.pressure_tolerance = 2.0
	var consumer := sim.make_organism(sim.sim_bounds.get_center(),genes,0,-1)
	consumer.energy = 10.0
	consumer.hunt_timer = 0.0
	sim.organisms.assign([consumer])
	if supplied:
		if role in ["Scavenger","Decomposer"]:
			sim._create_carcass(consumer.pos,40.0)
		else:
			var prey_genes := genes.duplicate(true)
			prey_genes.herbivory = 0.0
			prey_genes.predation = 0.0
			prey_genes.phototrophy = 1.0
			prey_genes.armor = 0.0
			var prey := sim.make_organism(consumer.pos,prey_genes,0,-1)
			prey.energy = 30.0
			prey.health = 0.1
			sim.organisms.append(prey)
	return sim
