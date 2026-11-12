extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	const UI = preload("res://scripts/ui_manager.gd")
	var chosen := {"planet":-1,"sector":-1}
	var galaxy := UI.show_galaxy(root, 0, func(index: int) -> void: chosen.planet = index)
	await process_frame
	var galaxy_view = galaxy.get_child(0).get_child(0).get_child(1)
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	click.position = galaxy_view.planet_position(4)
	galaxy_view.call("_gui_input", click)
	galaxy.find_child("ConfirmSelection", true, false).emit_signal("pressed")
	assert(chosen.planet == 4, "Galaxy selection must reach the experiment callback")
	var landing := UI.show_landing(root, chosen.planet, -1, func(index: int) -> void: chosen.sector = index)
	await process_frame
	var map = landing.get_child(0).get_child(0).get_child(1)
	var confirm: Button = landing.find_child("ConfirmSelection", true, false)
	assert(confirm.disabled, "Landing requires an explicit selection")
	assert(map.sector_at(Vector2(-1, -1)) == -1)
	click.position = map.size * Vector2(0.9, 0.9)
	map.call("_gui_input", click)
	assert(map.selected_sector == 11 and not confirm.disabled)
	var left := InputEventKey.new()
	left.pressed = true
	left.keycode = KEY_LEFT
	map.call("_gui_input", left)
	confirm.emit_signal("pressed")
	assert(chosen.sector == 10, "Keyboard navigation must affect confirmed landing")
	var sim := SimulationManager.new()
	sim.seed_world(1, chosen.planet, 99, chosen.sector)
	var point: Vector2 = sim.organisms[0].pos - sim.sim_bounds.position
	assert(int(point.x / sim.sim_bounds.size.x * 4.0) == 2 and int(point.y / sim.sim_bounds.size.y * 3.0) == 2, "Confirmed landing must seed the corresponding world sector")
	await process_frame
	print("World selection tests passed: galaxy click, landing click, keyboard navigation, confirmation and seeded sector")
	quit(0)
