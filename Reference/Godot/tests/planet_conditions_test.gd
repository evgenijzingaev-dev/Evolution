extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(34, 4, 101)
	var planet := PlanetManager.get_planet(4)
	var biomes := BiomeManager.biomes_for_planet(4)
	assert(is_equal_approx(float(planet.pressure) * float(biomes[1].pressure), 42.0), "Trench pressure must not square the planetary reference")
	assert(float(planet.sunlight) * float(biomes[2].light) > 0.0 and float(biomes[1].light) == 0.0, "Pelagic and abyssal regions must have different light availability")
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	genes.pressure_tolerance = 0.7
	var fragile: float = sim._environmental_stress(genes, planet, biomes[1], sim.environment.event_data())
	genes.pressure_tolerance = 2.2
	var adapted: float = sim._environmental_stress(genes, planet, biomes[1], sim.environment.event_data())
	assert(adapted < fragile, "Pressure tolerance must reduce physiological stress")
	for i in range(25):
		sim.step(0.2)
	assert(not sim.organisms.is_empty(), "Thalassa founders must have time to feed and undergo selection")
	sim.select_planet(0)
	for organism in sim.organisms:
		assert(organism.environment_timer == 0.0, "Changing planets must invalidate cached local conditions")
	print("Planet condition tests passed: pressure units, local light, adaptation, initial viability, cache invalidation")
	quit(0)
