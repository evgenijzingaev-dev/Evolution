extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	var cases := 0
	for planet_index in range(5):
		sim.seed_world(1,planet_index,101)
		var planet := sim.planet_data()
		for event_name in ["", "Drought", "Ice age", "Volcanic eruption"]:
			if not event_name.is_empty(): sim.environment.trigger(planet,0.0,event_name)
			var event := sim.environment.event_data()
			for x in range(10):
				for y in range(8):
					var position := sim.sim_bounds.position + Vector2((x + 0.5) / 10.0,(y + 0.5) / 8.0) * sim.sim_bounds.size
					var genes: Dictionary = sim.organisms[0].genes
					var biome := BiomeManager.at_position(position,sim.sim_bounds,planet_index)
					var stress := sim._environmental_stress(genes,planet,biome,event)
					var expected := _reference(sim,position,genes,planet,event,stress)
					assert(sim._migration_direction(position,genes,planet,event,stress) == expected)
					assert(sim._migration_direction(position,genes,planet,event,stress,str(biome.name)) == expected)
					cases += 1
	print("Migration equivalence tests passed: ",cases," locations/events across five planets match the unoptimized direction and tie order")
	quit(0)

func _reference(sim: SimulationManager,position: Vector2,genes: Dictionary,planet: Dictionary,event: Dictionary,current_stress: float) -> Vector2:
	var best := current_stress
	var result := Vector2.ZERO
	for direction in [Vector2.LEFT,Vector2.RIGHT,Vector2.UP,Vector2.DOWN]:
		var sample: Vector2 = position + direction * 90.0
		if not sim.sim_bounds.has_point(sample): continue
		var biome := BiomeManager.at_position(sample,sim.sim_bounds,sim.planet_index)
		var stress := sim._environmental_stress(genes,planet,biome,event)
		if stress < best:
			best = stress
			result = direction
	return result
