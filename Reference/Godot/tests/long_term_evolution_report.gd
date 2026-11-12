extends SceneTree

const OUTPUT := "res://tests/results/long_term_evolution.json"
const HORIZON := 3600.0
const Save = preload("res://scripts/save_manager.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var results: Array[Dictionary] = []
	for experiment_seed in [101,202,303]:
		var sim := SimulationManager.new()
		sim.seed_world(1,0,experiment_seed)
		# Same favorable sector as the short Terra refugia experiment.
		sim.organisms[0].pos = sim.sim_bounds.position + Vector2(0.625,0.5) * sim.sim_bounds.size
		var report := {"planet":"Terra","seed":experiment_seed,"horizon":HORIZON,"save_version":Save.SAVE_VERSION,"founders":1,"peak":1,"max_generation":0,"observed_forms":{},"samples":[],"finished":false}
		results.append(report)
		for tick in range(int(HORIZON / SimulationManager.TICK_SECONDS)):
			sim.step(SimulationManager.TICK_SECONDS)
			report.peak = maxi(int(report.peak),sim.organisms.size())
			if tick % 5 == 0:
				for o in sim.organisms:
					report.max_generation = maxi(int(report.max_generation),int(o.generation))
					var form := str(o.kind)
					if not report.observed_forms.has(form): report.observed_forms[form] = sim.elapsed
			if tick % 300 == 0 or sim.organisms.is_empty() or tick == int(HORIZON / SimulationManager.TICK_SECONDS) - 1:
				assert(sim.organisms.size() == 1 + sim.births - sim.deaths)
				report.samples.append(_sample(sim))
			if tick % 3000 == 2999:
				_write(results)
				print("Evolution report: seed ",experiment_seed," time ",roundi(sim.elapsed)," population ",sim.organisms.size()," generation ",report.max_generation)
			if sim.organisms.is_empty(): break
		report.finished = true
		report.final = _sample(sim)
		report.death_causes = sim.death_causes.duplicate(true)
		report.snapshot = "res://tests/results/long_term_seed_%d.json" % experiment_seed
		assert(Save.save_simulation(sim,report.snapshot) == OK)
		_write(results)
		print("Evolution result: ",JSON.stringify({"seed":experiment_seed,"elapsed":sim.elapsed,"population":sim.organisms.size(),"generation":report.max_generation,"forms":report.observed_forms}))
	print("Long-term evolution report complete: three one-cell Terra experiments, up to 3600 simulated seconds")
	quit(0)

func _sample(sim: SimulationManager) -> Dictionary:
	var roles := {}
	var bodies := {"single":0,"colonial":0,"differentiated":0}
	var gene_ranges := {}
	for o in sim.organisms:
		assert(is_finite(float(o.energy)) and is_finite(float(o.health)))
		var role := GeneticsSystem.trophic_role(o.genes)
		roles[role] = int(roles.get(role,0)) + 1
		var body_key := "single" if int(o.body.cell_count) == 1 else ("differentiated" if float(o.genes.differentiation) >= 0.5 else "colonial")
		bodies[body_key] += 1
		for gene in ["speed","efficiency","lifespan","phototrophy","chemotrophy","predation","herbivory","scavenging","decomposition","coloniality","differentiation","sexual_reproduction"]:
			var value := float(o.genes[gene])
			if not gene_ranges.has(gene): gene_ranges[gene] = [value,value]
			else:
				gene_ranges[gene][0] = minf(float(gene_ranges[gene][0]),value)
				gene_ranges[gene][1] = maxf(float(gene_ranges[gene][1]),value)
	return {"time":sim.elapsed,"population":sim.organisms.size(),"births":sim.births,"deaths":sim.deaths,"mutations":sim.mutations,"discovered_species":sim.discovered_species.size(),"living_species":sim.species_counts().size(),"roles":roles,"bodies":bodies,"gene_ranges":gene_ranges}

func _write(results: Array[Dictionary]) -> void:
	var file := FileAccess.open(OUTPUT,FileAccess.WRITE)
	file.store_string(JSON.stringify(results,"\t"))
	file.close()
