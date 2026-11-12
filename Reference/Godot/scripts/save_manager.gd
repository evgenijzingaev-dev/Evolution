class_name SaveManager
extends RefCounted

const SAVE_PATH := "user://evolution_save.json"
const AUTOSAVE_PATH := "user://evolution_autosave.json"
const SAVE_VERSION := 8

static func save_simulation(sim: SimulationManager, path: String = SAVE_PATH) -> Error:
	if sim._step_active:
		return ERR_BUSY
	var packed_organisms: Array[Dictionary] = []
	for o in sim.organisms:
		packed_organisms.append({"id":o.id,"pos":[o.pos.x,o.pos.y],"heading":o.heading,"genes":o.genes,"energy":o.energy,"age":o.age,"generation":o.generation,"parent":o.parent,"species":o.species,"behavior":o.behavior,"target_food":o.get("target_food", -1),"think_timer":o.get("think_timer", 0.0),"environment_timer":o.get("environment_timer", 0.0),"environmental_stress":o.get("environmental_stress", 0.0),"migration_direction":[o.get("migration_direction", Vector2.ZERO).x,o.get("migration_direction", Vector2.ZERO).y]})
		packed_organisms.back()["health"] = o.get("health", OrganismManager.max_health(o.genes))
		packed_organisms.back()["second_parent"] = o.get("second_parent", -1)
		packed_organisms.back()["reproduction_cooldown"] = o.get("reproduction_cooldown", 0.0)
		packed_organisms.back()["biome_cache"] = o.get("biome_cache", {})
		packed_organisms.back()["threat_timer"] = o.get("threat_timer", 0.0)
		packed_organisms.back()["hunt_timer"] = o.get("hunt_timer", 0.0)
		packed_organisms.back()["target_prey"] = o.get("target_prey", -1)
		var threat: Dictionary = o.get("threat_snapshot", {})
		if not threat.is_empty():
			packed_organisms.back()["threat_snapshot"] = {"id":threat.id,"pos":[threat.pos.x, threat.pos.y]}
	var packed_food: Array = []
	for p in sim.food:
		packed_food.append([p.x,p.y])
	var payload := {"version":SAVE_VERSION,"elapsed":sim.elapsed,"births":sim.births,"deaths":sim.deaths,"mutations":sim.mutations,"generation_sum":sim.generation_sum,"lifespan_sum":sim.lifespan_sum,"lifespan_count":sim.lifespan_count,"next_id":sim._next_id,"speed_index":sim.speed_index,"organisms":packed_organisms,"food":packed_food,"planet_index":sim.planet_index,"events":sim.environment.event_history,"timeline":sim.timeline,"species":sim.discovered_species,"statistics":sim.statistics_history,"stability":sim.ecosystem_stability,"active_event":sim.environment.event_data()}
	payload["settings"] = sim.settings.duplicate(true)
	var kinds: Array[String] = []
	for index in range(sim.food.size()): kinds.append(sim.resource_kind(index))
	payload["resource_kinds"] = kinds
	var reserves: Array[float] = []
	for index in range(sim.food.size()): reserves.append(sim.resource_reserve(index))
	payload["resource_reserves"] = reserves
	payload["planet_overrides"] = sim.planet_overrides.duplicate(true)
	payload["environment_dirty"] = sim._environment_dirty
	payload["death_causes"] = sim.death_causes.duplicate(true)
	payload["runtime"] = _pack_runtime(sim)
	if not _valid_payload(payload): return ERR_INVALID_DATA
	var encoded := JSON.stringify(payload, "", true, true).to_utf8_buffer()
	if encoded.size() > 134217728: return ERR_OUT_OF_MEMORY
	var temporary_path := path + ".tmp"
	var file := FileAccess.open(temporary_path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_buffer(encoded)
	file.flush()
	var write_error := file.get_error()
	file.close()
	if write_error != OK:
		return write_error
	return DirAccess.rename_absolute(temporary_path, path)

static func load_simulation(sim: SimulationManager, path: String = SAVE_PATH) -> Error:
	if sim._step_active:
		return ERR_BUSY
	if not FileAccess.file_exists(path):
		return ERR_FILE_NOT_FOUND
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return FileAccess.get_open_error()
	if file.get_length() > 134217728:
		file.close()
		return ERR_INVALID_DATA
	var parsed = JSON.parse_string(file.get_as_text())
	file.close()
	if not parsed is Dictionary or not _number(parsed.get("version")) or int(parsed.get("version", 0)) < 1 or int(parsed.get("version", 0)) > SAVE_VERSION:
		return ERR_INVALID_DATA
	if not _valid_payload(parsed):
		return ERR_INVALID_DATA
	sim.organisms.clear()
	for item in parsed.get("organisms", []):
		var p: Array = item.get("pos", [sim.sim_bounds.get_center().x,sim.sim_bounds.get_center().y])
		var genes: Dictionary = GeneticsSystem.complete_genome(item.get("genes", {}))
		var organism := OrganismManager.create(int(item.id), Vector2(float(p[0]),float(p[1])), genes, int(item.generation), int(item.parent), sim.rng)
		organism.heading = float(item.get("heading", 0.0))
		organism.behavior = str(item.get("behavior", "FORAGING"))
		organism.energy = float(item.get("energy", 18.0))
		organism.age = float(item.get("age", 0.0))
		organism.second_parent = int(item.get("second_parent", -1))
		organism.reproduction_cooldown = float(item.get("reproduction_cooldown", 0.0))
		organism.health = clampf(float(item.get("health", OrganismManager.max_health(genes))), 0.0, OrganismManager.max_health(genes))
		organism.species = str(item.get("species", GeneticsSystem.species_signature(genes)))
		organism.target_food = int(item.get("target_food", -1))
		organism.think_timer = float(item.get("think_timer", 0.0))
		organism.environment_timer = float(item.get("environment_timer", 0.0))
		organism.environmental_stress = float(item.get("environmental_stress", 0.0))
		organism.biome_cache = item.get("biome_cache", {}).duplicate(true)
		organism.threat_timer = float(item.get("threat_timer", 0.0))
		organism.hunt_timer = float(item.get("hunt_timer", 0.0))
		organism.target_prey = int(item.get("target_prey", -1))
		var threat: Dictionary = item.get("threat_snapshot", {})
		if not threat.is_empty():
			organism.threat_snapshot = {"id":int(threat.id),"pos":Vector2(float(threat.pos[0]),float(threat.pos[1]))}
		var migration: Array = item.get("migration_direction", [0.0, 0.0])
		organism.migration_direction = Vector2(float(migration[0]), float(migration[1]))
		sim.organisms.append(organism)
	sim.food.clear()
	sim.resource_kinds.clear()
	sim.resource_reserves.clear()
	for p in parsed.get("food", []):
		sim.food.append(Vector2(float(p[0]),float(p[1])))
		var index := sim.food.size() - 1
		sim.resource_kinds.append(str(parsed.resource_kinds[index]) if parsed.has("resource_kinds") else "nutrient")
		sim.resource_reserves.append(float(parsed.resource_reserves[index]) if parsed.has("resource_reserves") else 8.0)
	for key in ["elapsed","births","deaths","mutations","generation_sum","lifespan_sum","lifespan_count"]:
		sim.set(key, parsed.get(key, sim.get(key)))
	sim._next_id = int(parsed.get("next_id", 1))
	for organism in sim.organisms:
		sim._next_id = maxi(sim._next_id, int(organism.id) + 1)
	sim._selected_id = -1
	sim._next_statistics_time = sim.elapsed + 5.0
	sim._next_event_time = sim.elapsed + 90.0
	sim.speed_index = clampi(int(parsed.get("speed_index", 1)), 1, SimulationManager.SPEEDS.size() - 1)
	sim.planet_index = int(parsed.get("planet_index", 0))
	sim.planet_overrides = parsed.get("planet_overrides", {}).duplicate(true)
	sim.environment.event_history.clear()
	for event_item in parsed.get("events", []):
		sim.environment.event_history.append(event_item)
	sim.timeline.clear()
	for timeline_item in parsed.get("timeline", []):
		sim.timeline.append(timeline_item)
	sim.discovered_species = parsed.get("species", {})
	sim.death_causes = parsed.get("death_causes", {}).duplicate(true)
	for cause in sim.death_causes:
		sim.death_causes[cause] = int(sim.death_causes[cause])
	sim.statistics_history.clear()
	for statistics_item in parsed.get("statistics", []):
		sim.statistics_history.append(statistics_item)
	sim.ecosystem_stability = float(parsed.get("stability", 0.5))
	var active_event: Dictionary = parsed.get("active_event", {})
	sim.environment.current_event = str(active_event.get("name", ""))
	sim.environment.event_remaining = float(active_event.get("remaining", 0.0))
	sim.environment.temperature_shift = float(active_event.get("temperature_shift", 0.0))
	sim.environment.radiation_multiplier = float(active_event.get("radiation_multiplier", 1.0))
	sim.environment.food_multiplier = float(active_event.get("food_multiplier", 1.0))
	for key in ["oxygen_multiplier", "humidity_multiplier", "sunlight_multiplier"]:
		sim.environment.set(key, float(active_event.get(key, 1.0)))
	for key in ["water_shift", "toxicity_shift"]:
		sim.environment.set(key, float(active_event.get(key, 0.0)))
	sim._tick_accumulator = 0.0
	sim._step_active = false
	sim._step_index = 0
	sim._organism_grid_timer = 0.0
	sim._organism_grid.clear()
	sim._predator_grid.clear()
	sim._organisms_by_id.clear()
	sim._working_newborns.clear()
	sim._working_survivors.clear()
	sim.build_grid()
	sim._environment_dirty = bool(parsed.get("environment_dirty", false))
	sim.configure_settings(parsed.get("settings", {"mutation_rate":0.16,"mutation_strength":0.12,"resource_regrowth":1.0,"population_limit":10000}))
	_restore_runtime(sim, parsed.get("runtime", {}))
	return OK

static func _pack_runtime(sim: SimulationManager) -> Dictionary:
	var grid: Array = []
	var live_ids := {}
	for organism in sim.organisms: live_ids[int(organism.id)] = true
	for cell in sim._organism_grid:
		var ids: Array = []
		for organism in sim._organism_grid[cell]:
			if live_ids.has(int(organism.id)): ids.append(int(organism.id))
		grid.append({"cell":[cell.x,cell.y],"ids":ids})
	return {"rng_seed":str(sim.rng.seed),"rng_state":str(sim.rng.state),"environment_seed":str(sim.environment.rng.seed),"environment_state":str(sim.environment.rng.state),"next_event":sim._next_event_time,"next_statistics":sim._next_statistics_time,"grid_timer":sim._organism_grid_timer,"grid":grid,"frame":sim._frame,"accumulator":sim._tick_accumulator,"paused":sim.paused,"event_duration":sim.environment.event_duration,"bounds":[sim.sim_bounds.position.x,sim.sim_bounds.position.y,sim.sim_bounds.size.x,sim.sim_bounds.size.y]}

static func _restore_runtime(sim: SimulationManager, runtime: Dictionary) -> void:
	if runtime.is_empty(): return
	sim.rng.seed = int(runtime.rng_seed)
	sim.rng.state = int(runtime.rng_state)
	sim.environment.rng.seed = int(runtime.environment_seed)
	sim.environment.rng.state = int(runtime.environment_state)
	sim._next_event_time = float(runtime.next_event)
	sim._next_statistics_time = float(runtime.next_statistics)
	sim._organism_grid_timer = float(runtime.grid_timer)
	sim._frame = int(runtime.frame)
	sim._tick_accumulator = float(runtime.accumulator)
	sim.paused = bool(runtime.paused)
	sim.environment.event_duration = float(runtime.event_duration)
	sim.sim_bounds = Rect2(float(runtime.bounds[0]),float(runtime.bounds[1]),float(runtime.bounds[2]),float(runtime.bounds[3]))
	var by_id := {}
	for organism in sim.organisms: by_id[int(organism.id)] = organism
	for entry in runtime.grid:
		var cell := Vector2i(int(entry.cell[0]),int(entry.cell[1]))
		sim._organism_grid[cell] = []
		for id in entry.ids:
			var organism: Dictionary = by_id[int(id)]
			sim._organism_grid[cell].append(organism)
			if float(organism.genes.predation) > 0.55:
				if not sim._predator_grid.has(cell): sim._predator_grid[cell] = []
				sim._predator_grid[cell].append(organism)
	sim.build_grid()

static func _number(value: Variant) -> bool:
	return (value is int or value is float) and is_finite(float(value))

static func _pair(value: Variant) -> bool:
	return value is Array and value.size() == 2 and _number(value[0]) and _number(value[1]) and absf(float(value[0])) <= 1000000000.0 and absf(float(value[1])) <= 1000000000.0

static func _valid_payload(data: Dictionary) -> bool:
	if not preload("res://scripts/save_schema.gd").validate(data): return false
	if not data.get("environment_dirty", false) is bool: return false
	if not data.get("planet_overrides", {}) is Dictionary: return false
	for planet_key in data.get("planet_overrides", {}):
		if not str(planet_key).is_valid_int() or int(planet_key) < 0 or int(planet_key) >= PlanetManager.PLANETS.size(): return false
		var overrides = data.planet_overrides[planet_key]
		if not overrides is Dictionary: return false
		for key in overrides:
			if not PlanetManager.PARAMETER_RANGES.has(key) or not _number(overrides[key]): return false
			var bounds: Array = PlanetManager.PARAMETER_RANGES[key]
			if float(overrides[key]) < float(bounds[0]) or float(overrides[key]) > float(bounds[1]): return false
	if not data.get("death_causes", {}) is Dictionary: return false
	for value in data.get("death_causes", {}).values():
		if not _number(value) or float(value) < 0.0: return false
	if not data.get("settings", {}) is Dictionary:
		return false
	for value in data.get("settings", {}).values():
		if not _number(value):
			return false
	if not data.get("organisms") is Array or not data.get("food") is Array:
		return false
	if data.organisms.size() > SimulationManager.MAX_POPULATION or data.food.size() > 100000:
		return false
	for key in ["elapsed", "births", "deaths", "mutations", "generation_sum", "lifespan_sum", "lifespan_count", "next_id", "speed_index", "planet_index", "stability"]:
		if data.has(key) and not _number(data[key]):
			return false
	var planet := int(data.get("planet_index", 0))
	if planet < 0 or planet >= PlanetManager.PLANETS.size():
		return false
	var ids := {}
	for item in data.organisms:
		if not item is Dictionary or not _pair(item.get("pos")) or not item.get("genes") is Dictionary:
			return false
		for key in ["id", "heading", "energy", "age", "generation", "parent"]:
			if not _number(item.get(key)):
				return false
		if int(item.id) < 1 or ids.has(int(item.id)):
			return false
		ids[int(item.id)] = true
		for value in item.genes.values():
			if not _number(value):
				return false
		for key in ["target_food", "think_timer", "environment_timer", "environmental_stress", "health", "second_parent", "reproduction_cooldown", "threat_timer", "hunt_timer", "target_prey"]:
			if item.has(key) and not _number(item[key]):
				return false
		if item.has("migration_direction") and not _pair(item.migration_direction):
			return false
		if not item.get("biome_cache", {}) is Dictionary or not item.get("threat_snapshot", {}) is Dictionary:
			return false
		var biome: Dictionary = item.get("biome_cache", {})
		if not biome.is_empty():
			for key in ["temperature", "pressure", "oxygen", "light", "food", "geology"]:
				if not _number(biome.get(key)): return false
			for key in ["humidity", "water", "radiation_multiplier", "toxicity_multiplier"]:
				if biome.has(key) and not _number(biome[key]): return false
		var threat: Dictionary = item.get("threat_snapshot", {})
		if not threat.is_empty() and (not _number(threat.get("id")) or not _pair(threat.get("pos"))):
			return false
	if not _valid_runtime(data.get("runtime", {}), ids):
		return false
	for point in data.food:
		if not _pair(point):
			return false
	for key in ["events", "timeline", "statistics"]:
		if not data.get(key, []) is Array:
			return false
		for record in data.get(key, []):
			if not record is Dictionary:
				return false
	if not data.get("species", {}) is Dictionary or not data.get("active_event", {}) is Dictionary:
		return false
	for record in data.get("species", {}).values():
		if not record is Dictionary:
			return false
		for key in ["first_seen", "generation", "population", "extinct_at"]:
			if not _number(record.get(key)):
				return false
	for key in ["remaining", "temperature_shift", "radiation_multiplier", "food_multiplier", "oxygen_multiplier", "humidity_multiplier", "sunlight_multiplier", "water_shift", "toxicity_shift"]:
		if data.get("active_event", {}).has(key) and not _number(data.active_event[key]):
			return false
		if data.get("active_event", {}).has(key) and absf(float(data.active_event[key])) > 1000000.0: return false
	for key in ["remaining", "radiation_multiplier", "food_multiplier", "oxygen_multiplier", "humidity_multiplier", "sunlight_multiplier", "toxicity_shift"]:
		if data.get("active_event", {}).has(key) and float(data.active_event[key]) < 0.0: return false
	return true

static func _valid_runtime(runtime: Variant, ids: Dictionary) -> bool:
	if not runtime is Dictionary: return false
	if runtime.is_empty(): return true
	for key in ["rng_seed", "rng_state", "environment_seed", "environment_state"]:
		if not runtime.get(key) is String or not str(runtime[key]).is_valid_int(): return false
		if str(runtime[key]).length() > 20 or str(int(runtime[key])) != str(runtime[key]): return false
	for key in ["next_event", "next_statistics", "grid_timer", "frame", "accumulator", "event_duration"]:
		if not _number(runtime.get(key)): return false
		if float(runtime[key]) < 0.0: return false
	if not preload("res://scripts/save_schema.gd").integer(runtime.frame): return false
	if not runtime.get("paused") is bool or not runtime.get("bounds") is Array or runtime.bounds.size() != 4:
		return false
	for value in runtime.bounds:
		if not _number(value) or absf(float(value)) > 10000000.0: return false
	if float(runtime.bounds[2]) <= 0.0 or float(runtime.bounds[3]) <= 0.0: return false
	if not runtime.get("grid") is Array or runtime.grid.size() > SimulationManager.CELLS.x * SimulationManager.CELLS.y: return false
	var seen := {}
	var cells_seen := {}
	for entry in runtime.grid:
		if not entry is Dictionary or not _pair(entry.get("cell")) or not entry.get("ids") is Array: return false
		for coordinate in entry.cell:
			if not preload("res://scripts/save_schema.gd").integer(coordinate): return false
		if entry.cell[0] < 0 or entry.cell[0] >= SimulationManager.CELLS.x or entry.cell[1] < 0 or entry.cell[1] >= SimulationManager.CELLS.y: return false
		var cell_key := Vector2i(int(entry.cell[0]), int(entry.cell[1]))
		if cells_seen.has(cell_key): return false
		cells_seen[cell_key] = true
		for id in entry.ids:
			if not preload("res://scripts/save_schema.gd").integer(id, 1.0) or not ids.has(int(id)) or seen.has(int(id)): return false
			seen[int(id)] = true
	return true
