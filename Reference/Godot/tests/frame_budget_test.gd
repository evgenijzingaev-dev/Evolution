extends SceneTree

class SlowPreparation extends SimulationManager:
	var process_calls := 0
	func _begin_step(dt: float) -> void:
		super._begin_step(dt)
		OS.delay_usec(TICK_BUDGET_USEC * 2)
	func _process_step_chunk(chunk_size: int,time_budget_usec: int = 0) -> void:
		process_calls += 1
		super._process_step_chunk(chunk_size,time_budget_usec)

class SlowProcessing extends SimulationManager:
	var commits := 0
	func _process_step_chunk(chunk_size: int,time_budget_usec: int = 0) -> void:
		var had_work := _step_index < organisms.size()
		super._process_step_chunk(chunk_size,time_budget_usec)
		if had_work: OS.delay_usec(TICK_BUDGET_USEC * 2)
	func _finish_step() -> void:
		super._finish_step()
		commits += 1

func _initialize() -> void:
	for maximum in [false,true]:
		var prep := SlowPreparation.new()
		prep.seed_world(1,0,17)
		prep.speed_index = SimulationManager.MAX_SPEED_INDEX if maximum else 1
		prep.tick(0.2)
		assert(prep._step_active and prep.process_calls == 0, "Expensive grid preparation must not be followed by organism work in the same exhausted frame")
		prep.paused = true
		prep.tick(0.2)
		assert(prep.process_calls == 0)
		prep.paused = false
		prep.speed_index = 1
		prep.tick(0.0)
		assert(not prep._step_active and prep._frame == 1, "Prepared steps must resume without repeating preparation")
		var processing := SlowProcessing.new()
		processing.seed_world(1,0,17)
		processing.speed_index = SimulationManager.MAX_SPEED_INDEX if maximum else 1
		processing.tick(0.2)
		assert(processing._step_active and processing._step_index >= processing.organisms.size() and processing.commits == 0, "Heavy organism work must defer the commit to another frame")
		processing.speed_index = 1
		processing.tick(0.0)
		assert(not processing._step_active and processing.commits == 1 and processing._frame == 1)
	for maximum in [false,true]:
		var large := SimulationManager.new()
		large.seed_world(1200,0,17)
		large.speed_index = SimulationManager.MAX_SPEED_INDEX if maximum else 1
		large.tick(0.2)
		while large._step_index < large.organisms.size(): large.tick(0.0)
		assert(large._step_active, "Large-population commits must receive their own work slice")
		large.tick(0.0)
		assert(not large._step_active and large._frame == 1)
	print("Frame budget tests passed: expensive preparation and commit separation, dedicated large-population commit, pause and exact partial-step resume in normal/maximum modes")
	quit(0)
