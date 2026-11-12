extends SceneTree

const Save = preload("res://scripts/save_manager.gd")
const PATH := "user://maximum_speed_test.json"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var fast := SimulationManager.new()
	fast.seed_world(20,0,77)
	var reference := SimulationManager.new()
	reference.seed_world(20,0,77)
	for world in [fast,reference]:
		world.organisms[0].energy = 30.0
		world.organisms[0].genes.reproduction_rate = 1000.0
	fast.speed_index = SimulationManager.MAX_SPEED_INDEX
	# A generous test budget makes the exact four-step limit deterministic.
	fast._tick_maximum(1000000,4)
	assert(fast._frame == 4 and not fast._step_active)
	for tick in range(4): reference.step(SimulationManager.TICK_SECONDS)
	assert(fast.elapsed == reference.elapsed and fast.rng.state == reference.rng.state)
	assert(fast.environment.rng.state == reference.environment.rng.state)
	assert(fast.births > 0 and fast.births == reference.births and fast.mutations == reference.mutations, "Maximum speed must preserve reproduction and inheritance")
	for index in range(fast.organisms.size()):
		assert(fast.organisms[index].pos == reference.organisms[index].pos)
		assert(fast.organisms[index].energy == reference.organisms[index].energy)
	var before := fast.elapsed
	fast._tick_maximum(0,64)
	assert(fast.elapsed == before, "Exhausted frame budgets must not start new steps")
	fast.paused = true
	fast.tick(1.0)
	assert(fast.elapsed == before, "Pause must stop maximum acceleration")
	assert(Save.save_simulation(fast,PATH) == OK)
	var loaded := SimulationManager.new()
	assert(Save.load_simulation(loaded,PATH) == OK and loaded.speed_index == SimulationManager.MAX_SPEED_INDEX and loaded.paused)
	loaded.paused = false
	loaded.tick(1.0 / 60.0)
	assert(loaded.elapsed > before and loaded._tick_accumulator == 0.0)
	# Ordinary multipliers still accumulate real time at the requested rate.
	var normal := SimulationManager.new()
	normal.seed_world(1,0,77)
	normal.tick(0.1)
	assert(normal._frame == 0)
	normal.tick(0.1)
	assert(normal._frame == 1)
	for multiplier in [2,3,4]:
		normal.speed_index = multiplier
		normal.tick(SimulationManager.TICK_SECONDS / SimulationManager.SPEEDS[multiplier])
		assert(normal._frame == multiplier)
	# Maximum mode can resume a partial chunk without restarting its fixed step.
	fast.paused = false
	fast._begin_step(SimulationManager.TICK_SECONDS)
	fast._process_step_chunk(3)
	var partial_frame := fast._frame
	fast._tick_maximum(1000000,1)
	assert(fast._frame == partial_frame and not fast._step_active)
	var main: Node = load("res://scenes/main.tscn").instantiate()
	root.add_child(main)
	await process_frame
	main.sim.speed_index = 4
	main._on_speed()
	assert(main.sim.speed_index == SimulationManager.MAX_SPEED_INDEX and main.speed_button.text == "МАКС")
	main._on_speed()
	assert(main.sim.speed_index == 1, "The speed selector must wrap maximum mode to normal speed")
	main.queue_free()
	await process_frame
	DirAccess.remove_absolute(PATH)
	print("Maximum speed tests passed: bounded multi-step execution, fixed-step equivalence, pause, persistence, ordinary multipliers, partial-step resume and Russian UI control")
	quit(0)
