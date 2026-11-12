extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var results: Array[Dictionary] = []
	for planet_index in range(5):
		for experiment_seed in [101, 202, 303]:
			var sim := SimulationManager.new()
			sim.seed_world(1, planet_index, experiment_seed)
			var planet := PlanetManager.get_planet(planet_index)
			var genes: Dictionary = sim.organisms[0].genes
			var best_score := -INF
			var chosen := 0
			var chosen_biome := ""
			for sector in range(12):
				var point := sim.sim_bounds.position + Vector2((float(sector % 4) + 0.5) * sim.sim_bounds.size.x / 4.0, (float(sector / 4) + 0.5) * sim.sim_bounds.size.y / 3.0)
				var biome := BiomeManager.at_position(point, sim.sim_bounds, planet_index)
				var stress: float = sim._environmental_stress(genes, planet, biome, sim.environment.event_data())
				var score := float(biome.food) * 0.3 + float(genes.phototrophy) * float(planet.sunlight) * float(biome.light) + float(genes.chemotrophy) * float(planet.geology) * float(biome.geology) - stress
				if score > best_score:
					best_score = score
					chosen = sector
					chosen_biome = str(biome.name)
			sim.organisms[0].pos = sim.sim_bounds.position + Vector2((float(chosen % 4) + 0.5) * sim.sim_bounds.size.x / 4.0, (float(chosen / 4) + 0.5) * sim.sim_bounds.size.y / 3.0)
			for tick in range(3000):
				sim.step(0.2)
				if sim.organisms.is_empty(): break
			var record := {"planet":planet.name,"seed":experiment_seed,"biome":chosen_biome,"sector":chosen,"elapsed":sim.elapsed,"population":sim.organisms.size(),"births":sim.births,"death_causes":sim.death_causes}
			results.append(record)
			print(JSON.stringify(record))
	var file := FileAccess.open("res://tests/results/refugia_balance.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(results, "\t"))
	file.close()
	quit(0)
