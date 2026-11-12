extends SceneTree

const Archive = preload("res://scripts/experiment_archive.gd")
const Save = preload("res://scripts/save_manager.gd")
const DIRECTORY := "user://archive_test"
const LATEST := "user://archive_latest_test.json"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(2, 0, 78)
	var first := Archive.save_snapshot(sim, "Первый опыт", DIRECTORY, LATEST)
	assert(int(first.error) == OK)
	sim.elapsed = 40.0
	var second := Archive.save_snapshot(sim, "Второй опыт", DIRECTORY, LATEST)
	assert(int(second.error) == OK and first.path != second.path)
	var entries := Archive.entries(DIRECTORY, false)
	assert(entries.size() == 2 and entries[0].label == "Второй опыт")
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, first.path) == OK and restored.elapsed == 0.0)
	assert(Save.load_simulation(restored, LATEST) == OK and restored.elapsed == 40.0)
	var scene: Node = load("res://scenes/main.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var ui_sim: SimulationManager = scene.get("sim")
	while ui_sim.organisms.size() < 1500:
		var point := ui_sim.random_point()
		ui_sim.spawn(point.x, point.y, GeneticsSystem.starter(ui_sim.rng), 0, -1)
	ui_sim.paused = false
	ui_sim.tick(0.2)
	assert(ui_sim._step_active)
	ui_sim.paused = true
	scene.call("_request_load", first.path)
	for i in range(200):
		scene.call("_process_file_requests")
		if str(scene.get("_pending_load_path")).is_empty(): break
	assert(ui_sim.organisms.size() == 2 and ui_sim.elapsed == 0.0, "Loading must wait for the active step, even when paused")
	var window := preload("res://scripts/ui_manager.gd").show_archive(scene, func(_path: String) -> void: pass)
	assert(window.find_child("ArchiveEntries", true, false) != null)
	window.queue_free()
	scene.queue_free()
	for path in [first.path, second.path]:
		DirAccess.remove_absolute(path)
		DirAccess.remove_absolute(str(path) + ".meta.json")
	DirAccess.remove_absolute(LATEST)
	DirAccess.remove_absolute(DIRECTORY)
	await process_frame
	print("Archive tests passed: distinct snapshots, metadata listing, old/latest loading, paused pending-step loading and browser")
	quit(0)
