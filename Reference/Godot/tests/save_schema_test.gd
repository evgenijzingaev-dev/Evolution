extends SceneTree

const Save = preload("res://scripts/save_manager.gd")
const PATH := "user://schema_test.json"

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(2, 0, 71)
	sim.step(0.2)
	assert(Save.save_simulation(sim, PATH) == OK)
	var original: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PATH))
	var signature: String = original.species.keys()[0]
	var edits: Array[Callable] = [
		func(d: Dictionary) -> void: d.resource_reserves[0] = -1.0,
		func(d: Dictionary) -> void: d.resource_reserves = [],
		func(d: Dictionary) -> void: d.resource_reserves[0] = "bad",
		func(d: Dictionary) -> void: d.resource_reserves[0] = 1.0e100,
		func(d: Dictionary) -> void: d.organisms[0].genes.pressure_tolerance = 0.0,
		func(d: Dictionary) -> void: d.organisms[0].genes.armor = 2.0,
		func(d: Dictionary) -> void: d.organisms[0].id = 1.5,
		func(d: Dictionary) -> void: d.organisms[0].parent = {},
		func(d: Dictionary) -> void: d.organisms[0].pos = [1.0e100, 0.0],
		func(d: Dictionary) -> void: d.organisms[0].biome_cache = {"light":[]},
		func(d: Dictionary) -> void: d.species[signature].changes = {},
		func(d: Dictionary) -> void: d.species[signature].changes = [{"gene":"size","from":{},"to":1.0}],
		func(d: Dictionary) -> void: d.species[signature].genes = {"size":"bad"},
		func(d: Dictionary) -> void: d.statistics = [{"population":"bad"}],
		func(d: Dictionary) -> void: d.timeline = [{"time":[]}],
		func(d: Dictionary) -> void: d.active_event.name = {},
		func(d: Dictionary) -> void: d.active_event.food_multiplier = -1.0,
		func(d: Dictionary) -> void: d.runtime.rng_state = "999999999999999999999999999999",
		func(d: Dictionary) -> void: d.runtime.bounds = [0.0, 0.0, 1.0e100, 10.0],
		func(d: Dictionary) -> void: d.runtime.grid.append(d.runtime.grid[0].duplicate(true)),
	]
	var before_state := sim.rng.state
	var before_elapsed := sim.elapsed
	for edit in edits:
		var data := original.duplicate(true)
		edit.call(data)
		var file := FileAccess.open(PATH, FileAccess.WRITE)
		file.store_string(JSON.stringify(data,"",true,true))
		file.close()
		assert(Save.load_simulation(sim, PATH) == ERR_INVALID_DATA, "Malformed nested save must be rejected")
		assert(sim.organisms.size() == 2 and sim.rng.state == before_state and sim.elapsed == before_elapsed, "Rejected loads must leave the live simulation untouched")
	var a := GeneticsSystem.complete_genome({"size":1.0})
	var b := GeneticsSystem.complete_genome({"size":1.0})
	assert(a == b and a.size() == GeneticsSystem.TRAITS.size(), "Migration defaults must be deterministic and complete")
	DirAccess.remove_absolute(PATH)
	print("Save schema tests passed: 20 malformed nested payloads rejected without live-state changes; deterministic migration defaults")
	quit(0)
