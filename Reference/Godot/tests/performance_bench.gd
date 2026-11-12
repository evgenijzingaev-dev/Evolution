extends SceneTree

const Simulation = preload("res://scripts/simulation_manager.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var incremental = Simulation.new()
	var incremental_genome: Dictionary = incremental.organisms[0].genes.duplicate(true)
	while incremental.organisms.size() < 10000:
		var point: Vector2 = incremental.random_point()
		incremental.spawn(point.x, point.y, incremental_genome.duplicate(true), 1, 1)
	var chunk_start := Time.get_ticks_usec()
	incremental.tick(Simulation.TICK_SECONDS)
	var worst_chunk_ms := float(Time.get_ticks_usec() - chunk_start) / 1000.0
	assert(incremental._step_active and incremental._step_index <= 250, "high population work is split into bounded frame chunks")
	assert(SaveManager.save_simulation(incremental) == ERR_BUSY, "partial simulation steps are not saved")
	while incremental._step_active:
		chunk_start = Time.get_ticks_usec()
		incremental.tick(0.0)
		worst_chunk_ms = maxf(worst_chunk_ms, float(Time.get_ticks_usec() - chunk_start) / 1000.0)
	for tick_index in range(30):
		incremental.tick(Simulation.TICK_SECONDS)
		while incremental._step_active:
			chunk_start = Time.get_ticks_usec()
			incremental.tick(0.0)
			worst_chunk_ms = maxf(worst_chunk_ms, float(Time.get_ticks_usec() - chunk_start) / 1000.0)
	print("10,000-organism max incremental chunk over 31 steps: %.2f ms" % worst_chunk_ms)
	for target_population in [1000, 5000, 10000]:
		var sim = Simulation.new()
		var genome: Dictionary = sim.organisms[0].genes.duplicate(true)
		while sim.organisms.size() < target_population:
			var point: Vector2 = sim.random_point()
			sim.spawn(point.x, point.y, genome.duplicate(true), 1, 1)
		sim.step(Simulation.TICK_SECONDS)
		sim.step(Simulation.TICK_SECONDS)
		var start := Time.get_ticks_usec()
		for i in range(10):
			sim.step(Simulation.TICK_SECONDS)
		var elapsed_ms := float(Time.get_ticks_usec() - start) / 10000.0
		print("%d-organism average tick: %.2f ms; detail: %s" % [target_population, elapsed_ms, str(sim.last_profile)])
	quit(0)
