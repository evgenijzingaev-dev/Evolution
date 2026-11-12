extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("Visual review requires the graphical rendering driver")
		quit(1)
		return
	root.size = Vector2i(1440,900)
	root.gui_embed_subwindows = true
	var main: Node = load("res://scenes/main.tscn").instantiate()
	root.add_child(main)
	await process_frame
	assert(preload("res://scripts/save_manager.gd").load_simulation(main.sim,"res://tests/results/long_term_seed_202.json") == OK)
	var simulation_rng: int = main.sim.rng.state
	var environment_rng: int = main.sim.environment.rng.state
	main.experiment_setup.hide()
	main.experiment_started = true
	main.sim.paused = true
	main.sim.speed_index = SimulationManager.MAX_SPEED_INDEX
	main.sim._selected_id = main.sim.organisms[0].id
	main._refresh_ui()
	await _capture("review_world")
	main._show_history()
	await _capture("review_history")
	await _close_windows(main)
	main._show_genome()
	await _capture("review_genome")
	for child in main.get_children():
		if child is Window:
			var text: RichTextLabel = child.get_child(0)
			text.get_v_scroll_bar().value = text.get_v_scroll_bar().max_value
	await _capture("review_genome_adaptations")
	await _close_windows(main)
	main._show_lineage()
	await _capture("review_species")
	await _close_windows(main)
	main._show_statistics()
	await _capture("review_statistics")
	for child in main.get_children():
		if child is Window: child.close_requested.emit()
	await process_frame
	for child in main.get_children(): assert(not child is Window,"Statistics must close on its window-close action")
	preload("res://scripts/ui_manager.gd").show_planet_settings(main,main.sim,main.sim.planet_index)
	await _capture("review_climate")
	await _close_windows(main)
	preload("res://scripts/ui_manager.gd").show_archive(main,func(_path: String) -> void: pass)
	await _capture("review_archive")
	await _close_windows(main)
	root.size = Vector2i(1280,800)
	await _capture("review_world_1280")
	main._show_experiment_setup()
	await _capture("review_setup_1280")
	assert(main.sim.rng.state == simulation_rng and main.sim.environment.rng.state == environment_rng, "Visual rendering must not consume simulation randomness")
	print("Visual review captures completed: rich world, history, genome, climate, archive and smaller-window setup")
	quit(0)

func _capture(name: String) -> void:
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	var capture := root.get_texture().get_image()
	assert(not capture.is_empty())
	assert(capture.save_png("res://tests/results/" + name + ".png") == OK)

func _close_windows(main: Node) -> void:
	for child in main.get_children():
		if child is Window: child.queue_free()
	await process_frame
