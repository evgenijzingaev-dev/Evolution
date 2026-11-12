extends SceneTree

const Save = preload("res://scripts/save_manager.gd")
const MANUAL := "user://integrity_manual_test.json"
const AUTO := "user://integrity_auto_test.json"

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(2, 0, 77)
	assert(Save.save_simulation(sim, MANUAL) == OK)
	sim.elapsed = 42.0
	assert(Save.save_simulation(sim, MANUAL) == OK, "Existing save must be replaceable")
	sim.elapsed = 99.0
	assert(Save.save_simulation(sim, AUTO) == OK)
	assert(Save.load_simulation(sim, MANUAL) == OK and sim.elapsed == 42.0, "Autosave cannot overwrite manual save")
	assert(Save.load_simulation(sim, AUTO) == OK and sim.elapsed == 99.0)
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANUAL))
	data.organisms[0].energy = "corrupt"
	_write(data)
	var previous_genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	assert(Save.load_simulation(sim, MANUAL) == ERR_INVALID_DATA)
	assert(sim.elapsed == 99.0 and sim.organisms.size() == 2 and sim.organisms[0].genes == previous_genes, "Corrupt load must leave current simulation intact")
	data.organisms[0].energy = 18.0
	data.organisms[1].id = data.organisms[0].id
	_write(data)
	assert(Save.load_simulation(sim, MANUAL) == ERR_INVALID_DATA, "Duplicate organism IDs must be rejected")
	var valid: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(AUTO))
	valid.planet_overrides = {"0":{"pressure":-1.0}}
	_write(valid)
	assert(Save.load_simulation(sim, MANUAL) == ERR_INVALID_DATA, "Out-of-range planet overrides must be rejected")
	valid.planet_overrides = {}
	valid.runtime.rng_state = "invalid"
	_write(valid)
	assert(Save.load_simulation(sim, MANUAL) == ERR_INVALID_DATA, "Malformed random state must be rejected before mutation")
	valid.erase("runtime")
	valid.version = 5
	for item in valid.organisms:
		for key in GeneticsSystem.ADDITIONAL_DEFAULTS: item.genes.erase(key)
	_write(valid)
	assert(Save.load_simulation(sim, MANUAL) == OK, "Older saves without continuation metadata must still load")
	for key in GeneticsSystem.ADDITIONAL_DEFAULTS:
		assert(sim.organisms[0].genes[key] == GeneticsSystem.ADDITIONAL_DEFAULTS[key])
	_write({"version":{},"food":[],"organisms":[]})
	assert(Save.load_simulation(sim, MANUAL) == ERR_INVALID_DATA)
	DirAccess.remove_absolute(MANUAL)
	DirAccess.remove_absolute(AUTO)
	print("Save integrity tests passed: replacement, independent slots, corrupt-load preservation, duplicate IDs, malformed version")
	quit(0)

func _write(data: Dictionary) -> void:
	var file := FileAccess.open(MANUAL, FileAccess.WRITE)
	file.store_string(JSON.stringify(data))
	file.close()
