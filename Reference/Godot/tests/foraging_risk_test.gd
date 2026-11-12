extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 3, 303)
	sim.settings.resource_regrowth = 0.0
	sim.food.clear()
	sim.resource_kinds.clear()
	var organism: Dictionary = sim.organisms[0]
	organism.pos = Vector2(940,700)
	organism.genes.speed = 0.0
	organism.genes.temperature_tolerance = 1.0
	organism.genes.pressure_tolerance = 2.0
	organism.genes.gills = 1.0
	organism.genes.predation = 0.0
	organism.genes.herbivory = 0.0
	organism.energy = 12.0
	sim.food.append(Vector2(910,700)) # Closer, across the ash-desert boundary.
	sim.food.append(Vector2(980,700)) # Reachable without leaving the caldera lake.
	sim.resource_kinds.assign(["nutrient", "nutrient"])
	sim.step(0.2)
	assert(organism.target_food == 1, "Foraging must consider local physiological risk instead of repeatedly steering out of a refuge")
	organism.genes.speed = 25.0
	for tick in range(30): sim.step(0.2)
	assert(sim.food[1].x < 0.0 and not organism.dead, "The safer selected resource must actually be reached and eaten")
	# Genetically tolerant organisms need not avoid the closer hot sector.
	organism.pos = Vector2(940,700)
	organism.genes.speed = 0.0
	organism.genes.temperature_tolerance = 2.2
	organism.genes.resilience = 1.0
	organism.environment_timer = 0.0
	organism.think_timer = 0.0
	organism.target_food = -1
	sim.food[0] = Vector2(910,700)
	sim.food[1] = Vector2(1030,700)
	sim.step(0.2)
	assert(organism.target_food == 0, "Risk is a genome-dependent cost, not an absolute biome prohibition")
	print("Foraging risk tests passed: refuge-aware target choice, actual consumption and genotype-dependent risk")
	quit(0)
