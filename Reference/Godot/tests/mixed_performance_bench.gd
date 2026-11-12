extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var results: Array[Dictionary] = []
	for population in [1000, 5000, 10000]:
		var sim := SimulationManager.new()
		sim.seed_world(1, 0, 171)
		sim.organisms.clear()
		for index in range(population):
			var genes := GeneticsSystem.starter(sim.rng)
			match index % 10:
				0, 1, 2: genes.predation = 0.9
				3, 4: genes.herbivory = 0.9
				5, 6: genes.phototrophy = 0.9
				7, 8: genes.decomposition = 0.9
				9:
					genes.coloniality = 0.9
					genes.differentiation = 0.9
			var point := sim.random_point()
			sim.spawn(point.x, point.y, genes, 0, -1)
		var worst := 0.0
		var chunks := 0
		var stage_totals := {"grids_ms":0.0,"organisms_ms":0.0,"commit_ms":0.0}
		var stage_maxima := {"grids_ms":0.0,"organisms_ms":0.0,"commit_ms":0.0}
		var started := Time.get_ticks_usec()
		for tick in range(6):
			var chunk_start := Time.get_ticks_usec()
			sim.tick(0.2)
			worst = maxf(worst, float(Time.get_ticks_usec() - chunk_start) / 1000.0)
			chunks += 1
			while sim._step_active:
				chunk_start = Time.get_ticks_usec()
				sim.tick(0.0)
				worst = maxf(worst, float(Time.get_ticks_usec() - chunk_start) / 1000.0)
				chunks += 1
			for stage in stage_totals:
				stage_totals[stage] += float(sim.last_profile[stage])
				stage_maxima[stage] = maxf(float(stage_maxima[stage]),float(sim.last_profile[stage]))
		assert(sim.organisms.size() == population + sim.births - sim.deaths)
		var result := {"initial_population":population,"final_population":sim.organisms.size(),"chunks":chunks,"worst_chunk_ms":worst,"mean_step_ms":float(Time.get_ticks_usec() - started) / 6000.0}
		var means := {}
		for stage in stage_totals: means[stage] = float(stage_totals[stage]) / 6.0
		result["stage_means"] = means
		result["stage_maxima"] = stage_maxima
		results.append(result)
		print(JSON.stringify(result))
	DirAccess.make_dir_recursive_absolute("res://tests/results")
	var file := FileAccess.open("res://tests/results/mixed_performance.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(results,"\t"))
	file.close()
	quit(0)
