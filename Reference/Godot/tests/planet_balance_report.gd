extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var results: Array[Dictionary] = []
	var arguments := OS.get_cmdline_user_args()
	var founder_count := 1 if "single" in arguments else 34
	for planet in range(5):
		for experiment_seed in [101, 202, 303]:
			var sim := SimulationManager.new()
			sim.seed_world(founder_count, planet, experiment_seed)
			sim.environment.rng.seed = experiment_seed
			var peak := sim.organisms.size()
			for tick in range(3000):
				sim.step(0.2)
				peak = maxi(peak, sim.organisms.size())
				assert(sim.organisms.size() <= SimulationManager.MAX_POPULATION)
				for organism in sim.organisms:
					assert(is_finite(float(organism.energy)) and is_finite(float(organism.health)))
				if sim.organisms.is_empty():
					break
			assert(sim.organisms.size() == founder_count + sim.births - sim.deaths, "Population accounting must remain exact")
			var record := {"founders":founder_count,"planet":PlanetManager.get_planet(planet).name,"seed":experiment_seed,"elapsed":sim.elapsed,"population":sim.organisms.size(),"peak":peak,"births":sim.births,"deaths":sim.deaths,"species":sim.discovered_species.size()}
			results.append(record)
			print(JSON.stringify(record))
	DirAccess.make_dir_recursive_absolute("res://tests/results")
	var file := FileAccess.open("res://tests/results/single_founder_balance.json" if founder_count == 1 else "res://tests/results/planet_balance.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(results, "\t"))
	file.close()
	print("Planet balance report complete: 15 experiments, up to 600 simulation seconds each")
	quit(0)
