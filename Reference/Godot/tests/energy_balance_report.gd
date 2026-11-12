extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var reports: Array[Dictionary] = []
	for case in [[1,202,2], [2,202,0], [3,303,3], [4,202,2]]:
		var sim := SimulationManager.new()
		sim.seed_world(1, case[0], case[1])
		sim.organisms[0].pos = sim.sim_bounds.position + Vector2((float(case[2] % 4) + 0.5) * sim.sim_bounds.size.x / 4.0, (float(case[2] / 4) + 0.5) * sim.sim_bounds.size.y / 3.0)
		var samples: Array[Dictionary] = []
		for tick in range(3000):
			sim.step(0.2)
			if tick % 150 == 0 or tick == 2999 or sim.organisms.is_empty() or (tick > 2250 and tick % 5 == 0 and sim.organisms.size() == 1): samples.append(_sample(sim))
			if sim.organisms.is_empty(): break
		var report := {"planet":sim.planet_data().name,"seed":case[1],"samples":samples,"deaths":sim.death_causes}
		reports.append(report)
		print(JSON.stringify(report))
	var file := FileAccess.open("res://tests/results/energy_balance.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(reports,"\t"))
	file.close()
	quit(0)

func _sample(sim: SimulationManager) -> Dictionary:
	var nutrients := 0
	var carcasses := 0
	for index in range(sim.food.size()):
		if sim.food[index].x < sim.sim_bounds.position.x: continue
		if sim.resource_kind(index) == "carcass": carcasses += 1
		else: nutrients += 1
	var energy := 0.0
	var stress := 0.0
	var nearest_distance := 0.0
	var passive_income := 0.0
	var metabolism := 0.0
	var organism_details: Array[Dictionary] = []
	var states := {}
	var biomes := {}
	for o in sim.organisms:
		energy += float(o.energy)
		stress += float(o.environmental_stress)
		states[o.behavior] = int(states.get(o.behavior,0)) + 1
		var biome := BiomeManager.at_position(o.pos, sim.sim_bounds, sim.planet_index)
		biomes[biome.name] = int(biomes.get(biome.name,0)) + 1
		var nearest := INF
		for point in sim.food:
			if point.x >= sim.sim_bounds.position.x: nearest = minf(nearest, o.pos.distance_to(point))
		nearest_distance += nearest if is_finite(nearest) else 10000.0
		var planet := sim.planet_data()
		passive_income += (float(o.genes.phototrophy) * float(planet.sunlight) * float(biome.light) * sim.environment.sunlight_multiplier * 0.42 + float(o.genes.chemotrophy) * float(planet.geology) * float(biome.geology) * 0.65) * float(o.body.feeding_multiplier)
		metabolism += BehaviorSystem.metabolism_drain(o.genes, o.environmental_stress, o.behavior) + float(o.body.upkeep)
		if sim.organisms.size() <= 3:
			var target := int(o.target_food)
			organism_details.append({"id":o.id,"pos":[o.pos.x,o.pos.y],"heading":o.heading,"target":target,"target_pos":[sim.food[target].x,sim.food[target].y] if target >= 0 else [],"target_distance":o.pos.distance_to(sim.food[target]) if target >= 0 else -1.0,"speed":o.genes.speed,"sensing":o.genes.sensing,"migration":[o.migration_direction.x,o.migration_direction.y],"age":o.age,"lifespan":o.genes.lifespan})
	var count := maxf(1.0, sim.organisms.size())
	return {"time":sim.elapsed,"population":sim.organisms.size(),"energy":energy/count,"stress":stress/count,"nearest_food":nearest_distance/count,"passive_income":passive_income/count,"metabolism":metabolism/count,"nutrients":nutrients,"carcasses":carcasses,"states":states,"biomes":biomes,"event":sim.environment.current_event,"details":organism_details}
