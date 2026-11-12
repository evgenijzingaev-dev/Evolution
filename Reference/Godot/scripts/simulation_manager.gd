class_name SimulationManager
extends RefCounted

const Reproduction = preload("res://scripts/reproduction_system.gd")

const MAX_POPULATION := 10000
const STARTING_POPULATION := 34
const FOOD_TARGET := 480
const TICK_SECONDS := 0.20
const CELLS := Vector2i(16, 12)
const FOOD_PER_CELL := 2

var rng := RandomNumberGenerator.new()
var organisms: Array[Dictionary] = []
var food: Array[Vector2] = []
var resource_kinds: Array[String] = []
var resource_reserves: Array[float] = []
var elapsed := 0.0
var births := 0
var deaths := 0
var death_causes: Dictionary = {}
var mutations := 0
var generation_sum := 0
var lifespan_sum := 0.0
var lifespan_count := 0
var paused := false
var settings := {"mutation_rate":0.16,"mutation_strength":0.12,"resource_regrowth":1.0,"population_limit":10000}
var speed_index := 1
var sim_bounds := Rect2(310, 126, 820, 650)
var planet_index := 0
var planet_overrides: Dictionary = {}
var _environment_dirty := false
var environment := EnvironmentSystem.new()
var timeline: Array[Dictionary] = []
var discovered_species: Dictionary = {}
var statistics_history: Array[Dictionary] = []
var ecosystem_stability := 0.5
var _next_statistics_time := 0.0
var _next_event_time := 90.0
var _tick_accumulator := 0.0
var _next_id := 1
var _grid: Dictionary = {}
var _resource_biomes: Array[Dictionary] = []
var _organism_grid: Dictionary = {}
var _predator_grid: Dictionary = {}
var _organisms_by_id: Dictionary = {}
var _frame := 0
var _selected_id := -1
var last_profile: Dictionary = {}
var _step_active := false
var _step_delta := TICK_SECONDS
var _step_index := 0
var _step_planet: Dictionary = {}
var _step_event: Dictionary = {}
var _working_newborns: Array[Dictionary] = []
var _working_survivors: Array[Dictionary] = []
var _step_profile_start := 0
var _step_profile_after_grids := 0
var _organism_grid_timer := 0.0

const SPEEDS := [0.0, 1.0, 2.0, 5.0, 10.0, 0.0]
const MAX_SPEED_INDEX := 5
const TICK_BUDGET_USEC := 8000
const MAX_STEPS_PER_TICK := 64

func _init() -> void:
	rng.randomize()
	seed_world()

func seed_world(founder_count: int = STARTING_POPULATION, selected_planet: int = 0, experiment_seed: int = -1, founding_sector: int = -1) -> void:
	if experiment_seed >= 0:
		rng.seed = experiment_seed
	else:
		rng.randomize()
	organisms.clear()
	food.clear()
	resource_kinds.clear()
	resource_reserves.clear()
	elapsed = 0.0
	births = 0
	deaths = 0
	death_causes.clear()
	mutations = 0
	generation_sum = 0
	lifespan_sum = 0.0
	lifespan_count = 0
	_next_id = 1
	_selected_id = -1
	_tick_accumulator = 0.0
	_step_active = false
	_step_index = 0
	_working_newborns.clear()
	_working_survivors.clear()
	_organism_grid_timer = 0.0
	_organism_grid.clear()
	_predator_grid.clear()
	_organisms_by_id.clear()
	paused = false
	speed_index = 1
	planet_index = posmod(selected_planet, PlanetManager.PLANETS.size())
	environment = EnvironmentSystem.new()
	if experiment_seed >= 0:
		environment.rng.seed = experiment_seed
	timeline.clear()
	discovered_species.clear()
	statistics_history.clear()
	ecosystem_stability = 0.5
	_next_statistics_time = 0.0
	_next_event_time = rng.randf_range(75.0, 125.0)
	for i in range(clampi(founder_count, 1, MAX_POPULATION)):
		var point := random_point()
		if founding_sector >= 0:
			var sector := clampi(founding_sector, 0, 11)
			point = sim_bounds.position + Vector2((float(sector % 4) + 0.5) * sim_bounds.size.x / 4.0, (float(sector / 4) + 0.5) * sim_bounds.size.y / 3.0)
			point += Vector2(rng.randf_range(-12.0, 12.0), rng.randf_range(-12.0, 12.0))
		spawn(point.x, point.y, GeneticsSystem.starter(rng), 0, -1)
	for o in organisms:
		_register_species(o, "abiogenesis")
	for i in range(FOOD_TARGET):
		food.append(random_point())
		resource_kinds.append("nutrient")
		resource_reserves.append(8.0)
	build_grid()

func set_bounds(bounds: Rect2) -> void:
	sim_bounds = bounds

func planet_data(index: int = -1) -> Dictionary:
	var chosen := planet_index if index < 0 else posmod(index, PlanetManager.PLANETS.size())
	var data := PlanetManager.get_planet(chosen)
	data.merge(planet_overrides.get(str(chosen), {}), true)
	return data

func configure_planet(index: int, values: Dictionary) -> void:
	var chosen := posmod(index, PlanetManager.PLANETS.size())
	var overrides: Dictionary = planet_overrides.get(str(chosen), {}).duplicate(true)
	for key in PlanetManager.PARAMETER_RANGES:
		if values.has(key) and (values[key] is float or values[key] is int) and is_finite(float(values[key])):
			var bounds: Array = PlanetManager.PARAMETER_RANGES[key]
			overrides[key] = clampf(float(values[key]), float(bounds[0]), float(bounds[1]))
	planet_overrides[str(chosen)] = overrides
	_environment_dirty = true
	for organism in organisms: organism.environment_timer = 0.0
	timeline.append({"name":"Planet conditions changed","detail":PlanetManager.get_planet(chosen).name,"time":elapsed})

func reset_planet_conditions(index: int) -> void:
	planet_overrides.erase(str(posmod(index, PlanetManager.PLANETS.size())))
	_environment_dirty = true
	for organism in organisms: organism.environment_timer = 0.0
	timeline.append({"name":"Planet conditions reset","detail":PlanetManager.get_planet(index).name,"time":elapsed})

func configure_settings(values: Dictionary) -> void:
	settings.mutation_rate = clampf(float(values.get("mutation_rate", settings.mutation_rate)), 0.0, 1.0)
	settings.mutation_strength = clampf(float(values.get("mutation_strength", settings.mutation_strength)), 0.0, 0.5)
	settings.resource_regrowth = clampf(float(values.get("resource_regrowth", settings.resource_regrowth)), 0.0, 5.0)
	settings.population_limit = clampi(int(values.get("population_limit", settings.population_limit)), 1, MAX_POPULATION)

func tick(dt: float) -> void:
	if paused:
		return
	if speed_index == MAX_SPEED_INDEX:
		_tick_accumulator = 0.0
		_tick_maximum()
		return
	var started := Time.get_ticks_usec()
	_tick_accumulator += dt * SPEEDS[speed_index]
	if not _step_active and _tick_accumulator >= TICK_SECONDS:
		_begin_step(TICK_SECONDS)
	if _step_active:
		var remaining := TICK_BUDGET_USEC - int(Time.get_ticks_usec() - started)
		if remaining <= 0: return
		var chunk_size := 250 if _step_index == 0 and organisms.size() > 1000 else 500
		var had_work := _step_index < organisms.size()
		_process_step_chunk(chunk_size, remaining)
		if _step_index >= organisms.size():
			if Time.get_ticks_usec() - started >= TICK_BUDGET_USEC or (had_work and organisms.size() > 1000): return
			_finish_step()
			_tick_accumulator = maxf(0.0, _tick_accumulator - TICK_SECONDS)

func _tick_maximum(time_budget_usec: int = TICK_BUDGET_USEC, step_limit: int = MAX_STEPS_PER_TICK) -> void:
	var started := Time.get_ticks_usec()
	var completed := 0
	while completed < step_limit and Time.get_ticks_usec() - started < time_budget_usec:
		if not _step_active:
			_begin_step(TICK_SECONDS)
		var remaining := time_budget_usec - int(Time.get_ticks_usec() - started)
		if remaining <= 0: return
		var chunk_size := 250 if _step_index == 0 and organisms.size() > 1000 else 500
		var had_work := _step_index < organisms.size()
		_process_step_chunk(chunk_size, remaining)
		if _step_index >= organisms.size():
			if Time.get_ticks_usec() - started >= time_budget_usec or (had_work and organisms.size() > 1000): return
			_finish_step()
			completed += 1
			if organisms.size() > 1000: return

func step(dt: float) -> void:
	_begin_step(dt)
	while _step_index < organisms.size():
		_process_step_chunk(maxi(1, organisms.size()))
	_finish_step()

func _begin_step(dt: float) -> void:
	elapsed += dt
	var previous_event := environment.current_event
	environment.step(dt)
	var environment_changed := previous_event != environment.current_event or _environment_dirty
	_environment_dirty = false
	if elapsed >= _next_event_time and environment.current_event.is_empty():
		var event_record := environment.trigger(planet_data(), elapsed)
		timeline.append(event_record)
		environment_changed = true
		_next_event_time = elapsed + rng.randf_range(75.0, 150.0)
	if environment_changed:
		for organism in organisms: organism.environment_timer = 0.0
	_frame += 1
	_step_delta = dt
	_step_index = 0
	_working_newborns.clear()
	_working_survivors.clear()
	_step_profile_start = Time.get_ticks_usec()
	_organisms_by_id.clear()
	for organism in organisms: _organisms_by_id[int(organism.id)] = organism
	build_grid()
	_organism_grid_timer = maxf(0.0, _organism_grid_timer - dt)
	if _organism_grid_timer <= 0.0:
		build_organism_grid()
		_organism_grid_timer = 0.4
	_step_profile_after_grids = Time.get_ticks_usec()
	_step_planet = planet_data()
	_step_event = environment.event_data()
	_step_active = true

func _process_step_chunk(chunk_size: int, time_budget_usec: int = 0) -> void:
	var started := Time.get_ticks_usec()
	var last_index := mini(organisms.size(), _step_index + chunk_size)
	while _step_index < last_index:
		_process_organism(organisms[_step_index], _step_delta, _step_planet, _step_event)
		_step_index += 1
		if time_budget_usec > 0 and _step_index % 16 == 0 and Time.get_ticks_usec() - started >= time_budget_usec:
			break

func _process_organism(o: Dictionary, dt: float, planet: Dictionary, event: Dictionary) -> void:
	if o.dead:
		return
	o.age += dt
	o.reproduction_cooldown = maxf(0.0, float(o.get("reproduction_cooldown", 0.0)) - dt)
	var genes: Dictionary = o.genes
	o.environment_timer = maxf(0.0, float(o.get("environment_timer", 0.0)) - dt)
	if o.environment_timer <= 0.0 or not o.has("biome_cache") or o.biome_cache.is_empty():
		o.biome_cache = BiomeManager.at_position(o.pos, sim_bounds, planet_index)
		o.environmental_stress = _environmental_stress(genes, planet, o.biome_cache, event)
		o.migration_direction = _migration_direction(o.pos, genes, planet, event, o.environmental_stress, str(o.biome_cache.name)) if o.environmental_stress > 0.45 else Vector2.ZERO
		o.environment_timer = 1.0
	var biome: Dictionary = o.biome_cache
	var environmental_stress: float = o.environmental_stress
	var desired := Vector2.ZERO
	var nearest_food := int(o.get("target_food", -1))
	var sensing_radius := float(genes.sensing + genes.eyes * 60.0 + genes.luminescence * 40.0 * (1.0 - minf(1.0, float(biome.light) * float(planet.sunlight))))
	var nearest_d2 := sensing_radius * sensing_radius
	var cell := grid_cell(o.pos)
	var nearest_prey: Dictionary = {}
	var nearest_prey_d2 := nearest_d2
	var nearest_threat: Dictionary = {}
	var nearest_threat_d2 := nearest_d2
	if float(genes.predation) < 0.55:
		o.threat_timer = maxf(0.0, float(o.get("threat_timer", 0.0)) - dt)
		if o.threat_timer <= 0.0:
			o.threat_snapshot = BehaviorSystem.nearby_threat(o, _predator_grid, cell, CELLS, float(genes.sensing) * 0.6)
			o.threat_timer = 0.8
		nearest_threat = o.get("threat_snapshot", {})
		if not nearest_threat.is_empty():
			nearest_threat_d2 = o.pos.distance_squared_to(nearest_threat.pos)
	o.hunt_timer = maxf(0.0, float(o.get("hunt_timer", 0.0)) - dt)
	var hunts: bool = (float(genes.predation) > 0.55 or float(genes.herbivory) > 0.65) and o.energy < 24.0
	if hunts and o.hunt_timer > 0.0:
		nearest_prey = _organisms_by_id.get(int(o.get("target_prey", -1)), {})
		if not nearest_prey.is_empty() and (nearest_prey.dead or o.pos.distance_squared_to(nearest_prey.pos) > sensing_radius * sensing_radius):
			nearest_prey = {}
	if hunts and o.hunt_timer <= 0.0:
		for x in range(maxi(0, cell.x - 2), mini(CELLS.x, cell.x + 3)):
			for y in range(maxi(0, cell.y - 2), mini(CELLS.y, cell.y + 3)):
				var bucket: Array = _organism_grid.get(Vector2i(x, y), [])
				for slot in range(mini(4, bucket.size())):
					var candidate: Dictionary = bucket[posmod(slot + _frame * 7 + int(o.id), bucket.size())]
					if int(candidate.id) == int(o.id) or candidate.species == o.species or float(candidate.energy) <= 0.0 or candidate.dead:
						continue
					var prey_genes: Dictionary = candidate.genes
					var prey_d2: float = o.pos.distance_squared_to(candidate.pos)
					if float(prey_genes.predation) > 0.55:
						if prey_d2 < nearest_threat_d2:
							nearest_threat_d2 = prey_d2
							nearest_threat = candidate
						continue
					elif float(genes.predation) > 0.55 and float(prey_genes.predation) >= float(genes.predation):
						continue
					elif float(genes.predation) <= 0.55 and float(genes.herbivory) > 0.65 and GeneticsSystem.trophic_role(prey_genes) != "Producer":
						continue
					if float(prey_genes.size) > float(genes.size) * 1.25:
						continue
					if prey_d2 < nearest_prey_d2:
						nearest_prey_d2 = prey_d2
						nearest_prey = candidate
		o.target_prey = int(nearest_prey.get("id", -1))
		o.hunt_timer = 0.8
	o.think_timer = maxf(0.0, float(o.get("think_timer", 0.0)) - dt)
	if nearest_food < 0 or nearest_food >= food.size() or food[nearest_food].x < sim_bounds.position.x:
		nearest_food = -1
		o.target_food = -1
	# Missing/depleted food must not bypass the bounded AI search frequency.
	if o.think_timer <= 0.0 or float(o.age) <= dt:
		nearest_food = -1
		var nearest_score := INF
		var food_stress_by_biome := {str(biome.name):environmental_stress}
		var carcass_preference := EcosystemSystem.resource_preference(genes, "carcass")
		for x in range(maxi(0, cell.x - 2), mini(CELLS.x, cell.x + 3)):
			for y in range(maxi(0, cell.y - 2), mini(CELLS.y, cell.y + 3)):
				for candidate in _grid.get(Vector2i(x,y), []):
					var d2: float = o.pos.distance_squared_to(food[candidate])
					if d2 >= sensing_radius * sensing_radius: continue
					var risk := 1.0
					if environmental_stress > 0.45:
						var food_biome: Dictionary = _resource_biomes[candidate]
						var biome_name := str(food_biome.name)
						if not food_stress_by_biome.has(biome_name):
							food_stress_by_biome[biome_name] = _environmental_stress(genes, planet, food_biome, event)
						risk += 6.0 * maxf(0.0, float(food_stress_by_biome[biome_name]) - environmental_stress)
					var preference := carcass_preference if resource_kind(candidate) == "carcass" else 1.0
					var score := d2 * risk / preference
					if score < nearest_score:
						nearest_score = score
						nearest_d2 = d2
						nearest_food = candidate
		o.target_food = nearest_food
		o.think_timer = 1.1 + float(int(o.id) % 5) * 0.2
	elif nearest_food >= 0:
		nearest_d2 = o.pos.distance_squared_to(food[nearest_food])
		if nearest_d2 > sensing_radius * sensing_radius:
			nearest_food = -1
			o.target_food = -1
	var threatened := not nearest_threat.is_empty() and float(genes.predation) < 0.55 and nearest_threat_d2 < float(genes.sensing) * float(genes.sensing) * 0.36
	var state := BehaviorSystem.choose_state(threatened, not nearest_prey.is_empty(), nearest_food >= 0, float(o.energy), float(genes.predation) > 0.55)
	o.behavior = state
	if state == "FORAGING" and nearest_food >= 0 and resource_kind(nearest_food) == "carcass":
		if float(genes.get("scavenging", 0.0)) > 0.6: o.behavior = "SCAVENGING"
		elif float(genes.decomposition) > 0.6: o.behavior = "DECOMPOSING"
	if state == "FLEEING":
		desired = (o.pos - nearest_threat.pos).normalized()
	elif state == "HUNTING" or state == "GRAZING":
		desired = (nearest_prey.pos - o.pos).normalized()
	elif state == "FORAGING" and nearest_d2 > 2.5:
		desired = (food[nearest_food] - o.pos).normalized()
	else:
		desired = Vector2(cos(o.heading), sin(o.heading)) * 0.55 + Vector2(rng.randf_range(-0.8, 0.8), rng.randf_range(-0.8, 0.8)) * 0.45
	if environmental_stress > 0.45 and o.migration_direction.length_squared() > 0.01:
		desired = (desired + o.migration_direction * minf(2.5, environmental_stress)).normalized()
		o.behavior = "MIGRATING"
	var sexual := float(genes.get("sexual_reproduction", 0.0)) >= 0.6
	var mate := {}
	if sexual and Reproduction.eligible(o) and o.behavior != "FLEEING" and o.behavior != "MIGRATING":
		mate = Reproduction.find_mate(o, _organism_grid, cell, CELLS, sensing_radius)
		o.behavior = "SEEKING_MATE"
		if not mate.is_empty():
			desired = (mate.pos - o.pos).normalized()
	o.heading = lerp_angle(o.heading, desired.angle(), minf(1.0, dt * 2.5))
	var move_speed := BehaviorSystem.movement_speed(genes, float(o.energy), o.body)
	var gravity_load := lerpf(float(planet.gravity), 1.0, clampf(float(planet.water) + float(event.get("water_shift", 0.0)), 0.0, 1.0) * 0.8)
	move_speed /= sqrt(maxf(0.1, gravity_load))
	o.pos += Vector2(cos(o.heading), sin(o.heading)) * move_speed * dt
	wrap_position(o)
	var photosynthetic_energy := float(genes.phototrophy) * float(planet.sunlight) * float(biome.light) * float(event.get("sunlight_multiplier", 1.0)) * 0.42
	var chemical_energy := float(genes.chemotrophy) * float(planet.geology) * float(biome.geology) * 0.65
	o.energy += (photosynthetic_energy + chemical_energy) * float(o.body.feeding_multiplier) * dt
	o.energy -= float(o.body.upkeep) * dt
	o.energy -= maxf(0.0, gravity_load - 1.0) * 0.02 * dt
	o.energy -= BehaviorSystem.metabolism_drain(genes, environmental_stress, str(o.behavior)) * dt
	o.energy -= (float(genes.luminescence) * 0.025 + float(genes.get("flagella", 0.1)) * 0.008 + float(genes.get("fins", 0.0)) * 0.008) * dt
	OrganismManager.recover(o, dt)
	if environmental_stress > 2.0:
		OrganismManager.damage(o, (environmental_stress - 2.0) * 0.12 * dt)
	if nearest_food >= 0 and o.pos.distance_squared_to(food[nearest_food]) < 150.0:
		var resource_energy := EcosystemSystem.resource_energy(genes, resource_kind(nearest_food), resource_reserve(nearest_food), float(o.body.feeding_multiplier))
		if EcosystemSystem.consume(food, nearest_food):
			_set_resource_reserve(nearest_food, 0.0)
			o.energy += resource_energy * (1.0 - minf(0.65, environmental_stress * 0.12))
		o.target_food = -1
	if not nearest_prey.is_empty():
		var prey: Dictionary = nearest_prey
		var prey_genome: Dictionary = prey.genes
		if not prey.dead and o.pos.distance_squared_to(prey.pos) < pow(5.0 + float(genes.size) * 2.0 + float(prey_genome.armor) * 4.0, 2.0):
			var attack_power := float(genes.predation) + rng.randf() * 0.45
			if attack_power > float(prey_genome.armor) + 0.15:
				if OrganismManager.damage(prey, 5.0 + float(genes.predation) * 4.0):
					var intake := minf(EcosystemSystem.carcass_reserve(prey), 7.0 * float(genes.efficiency) * (1.0 - minf(0.8, float(prey_genome.armor) * 0.35)))
					_kill_organism(prey, "predation", intake)
					o.energy += intake
			else:
				o.energy -= 1.5
				o.behavior = "RETREATING"
	o.energy = minf(o.energy, float(genes.get("max_energy", 34.0)))
	var ready: bool = not sexual or (Reproduction.eligible(o) and not mate.is_empty() and Reproduction.eligible(mate) and o.pos.distance_squared_to(mate.pos) <= 225.0)
	if ready and float(o.health) > 0.0 and BehaviorSystem.reproduction_roll(rng, float(o.energy), organisms.size() + _working_newborns.size(), int(settings.population_limit), dt, float(genes.efficiency), float(genes.get("reproduction_rate", 1.0))):
		var inherited: Dictionary = Reproduction.recombine(genes, mate.genes, rng) if sexual else genes
		var child_genes := GeneticsSystem.inherit(inherited, rng, float(settings.mutation_rate), float(settings.mutation_strength))
		var mutated := false
		for gene_key in GeneticsSystem.TRAITS:
			if absf(float(child_genes[gene_key]) - float(inherited[gene_key])) > 0.001:
				mutated = true
		var child := make_organism(o.pos + Vector2(rng.randf_range(-8, 8), rng.randf_range(-8, 8)), child_genes, int(o.generation) + 1, int(o.id))
		if Reproduction.provision_offspring(o, child, mate):
			if mutated: mutations += 1
			if sexual:
				child.second_parent = int(mate.id)
				child.generation = maxi(int(o.generation), int(mate.generation)) + 1
				mate.reproduction_cooldown = 8.0
				o.reproduction_cooldown = 8.0
				o.behavior = "MATING"
				mate.behavior = "MATING"
			_register_species(child, str(o.species), genes)
			_working_newborns.append(child)
			births += 1
			generation_sum += int(child.generation)
	if o.energy <= 0.0 or float(o.get("health", 1.0)) <= 0.0 or o.age >= float(genes.lifespan):
		_kill_organism(o, "starvation" if o.energy <= 0.0 else ("environment" if float(o.get("health", 1.0)) <= 0.0 else "age"))
	else:
		_working_survivors.append(o)

func _finish_step() -> void:
	var profile_after_organisms := Time.get_ticks_usec()
	var depleted: Array[int] = []
	for i in range(food.size()):
		if food[i].x < sim_bounds.position.x:
			depleted.append(i)
	for i in range(mini(2, depleted.size())):
		var slot := rng.randi_range(0, depleted.size() - 1)
		var regrowth_point := random_point()
		var regrowth_biome := BiomeManager.at_position(regrowth_point, sim_bounds, planet_index)
		if rng.randf() < 0.18 * float(_step_planet.food_rate) * float(regrowth_biome.food) * environment.food_multiplier * float(settings.resource_regrowth):
			food[depleted[slot]] = regrowth_point
			_set_resource_kind(depleted[slot], "nutrient")
		depleted.remove_at(slot)
	organisms.clear()
	for survivor in _working_survivors:
		if not survivor.dead and float(survivor.energy) > 0.0:
			organisms.append(survivor)
	for child in _working_newborns:
		organisms.append(child)
	var live_counts := species_counts()
	for signature in discovered_species:
		var record: Dictionary = discovered_species[signature]
		var count := int(live_counts.get(signature, 0))
		record.population = count
		if count == 0 and float(record.extinct_at) < 0.0 and elapsed > float(record.first_seen) + 1.0:
			record.extinct_at = elapsed
			timeline.append({"name":"Species extinction", "detail":signature,"time":elapsed})
	if elapsed >= _next_statistics_time:
		_record_statistics()
		_next_statistics_time = elapsed + 5.0
	last_profile = {"grids_ms":float(_step_profile_after_grids - _step_profile_start) / 1000.0,"organisms_ms":float(profile_after_organisms - _step_profile_after_grids) / 1000.0,"commit_ms":float(Time.get_ticks_usec() - profile_after_organisms) / 1000.0}
	_step_active = false

func _record_statistics() -> void:
	var species := species_counts()
	var producer_count := 0
	var grazer_count := 0
	var predator_count := 0
	var decomposer_count := 0
	var omnivore_count := 0
	var scavenger_count := 0
	var energy_total := 0.0
	for o in organisms:
		energy_total += float(o.energy)
		match GeneticsSystem.trophic_role(o.genes):
			"Producer": producer_count += 1
			"Grazer": grazer_count += 1
			"Predator": predator_count += 1
			"Decomposer": decomposer_count += 1
			"Omnivore": omnivore_count += 1
			"Scavenger": scavenger_count += 1
	var population := organisms.size()
	if not statistics_history.is_empty():
		var previous_population := float(statistics_history.back().population)
		var relative_change := absf(float(population) - previous_population) / maxf(1.0, previous_population)
		ecosystem_stability = lerpf(ecosystem_stability, clampf(1.0 - relative_change, 0.0, 1.0), 0.12)
	statistics_history.append({"time":elapsed,"population":population,"species":species.size(),"producers":producer_count,"grazers":grazer_count,"predators":predator_count,"decomposers":decomposer_count,"omnivores":omnivore_count,"scavengers":scavenger_count,"mean_energy":energy_total / maxf(1.0, population)})
	if statistics_history.size() > 180:
		statistics_history.pop_front()

func make_organism(pos: Vector2, genes: Dictionary, generation: int, parent_id: int) -> Dictionary:
	var o := OrganismManager.create(_next_id, pos, genes, generation, parent_id, rng)
	_next_id += 1
	return o

func spawn(x: float, y: float, genes: Dictionary, generation: int, parent_id: int) -> void:
	organisms.append(make_organism(Vector2(x, y), genes, generation, parent_id))

func build_grid() -> void:
	_grid = EcosystemSystem.build_nutrient_grid(food, sim_bounds, CELLS, FOOD_PER_CELL, resource_kinds)
	_resource_biomes.clear()
	for point in food:
		_resource_biomes.append(BiomeManager.at_position(point, sim_bounds, planet_index))

func build_organism_grid() -> void:
	_organism_grid.clear()
	_predator_grid.clear()
	for i in range(organisms.size()):
		if organisms[i].energy <= 0.0 or organisms[i].dead:
			continue
		var cell := grid_cell(organisms[i].pos)
		if not _organism_grid.has(cell):
			_organism_grid[cell] = []
		_organism_grid[cell].append(organisms[i])
		if float(organisms[i].genes.predation) > 0.55:
			if not _predator_grid.has(cell):
				_predator_grid[cell] = []
			_predator_grid[cell].append(organisms[i])

func grid_cell(pos: Vector2) -> Vector2i:
	return EcosystemSystem.grid_cell(pos, sim_bounds, CELLS)

func _environmental_stress(genes: Dictionary, planet: Dictionary, biome: Dictionary, event: Dictionary) -> float:
	var temperature := float(planet.temperature) + float(biome.temperature) + float(event.temperature_shift)
	var pressure := float(planet.pressure) * float(biome.pressure)
	var oxygen := float(planet.oxygen) * float(biome.oxygen) * float(event.get("oxygen_multiplier", 1.0))
	var thermal_stress := absf(temperature) / (58.0 * float(genes.temperature_tolerance))
	var radiation_stress := float(planet.radiation) * float(event.radiation_multiplier) * float(biome.get("radiation_multiplier", 1.0)) / (1.2 * float(genes.radiation_tolerance))
	var pressure_stress := log(1.0 + pressure) / (1.4 * float(genes.pressure_tolerance))
	var oxygen_stress := maxf(0.0, 1.0 - oxygen * float(genes.gills + 0.2))
	var humidity := clampf(float(biome.get("humidity", planet.humidity)) * float(event.get("humidity_multiplier", 1.0)), 0.0, 1.0)
	var water := clampf(float(biome.get("water", planet.water)) + float(event.get("water_shift", 0.0)), 0.0, 1.0)
	var dehydration := (1.0 - humidity) * (1.0 - water) * (1.0 - float(genes.armor) * 0.6) * 0.35
	var toxicity := (float(planet.get("toxicity", 0.0)) * float(biome.get("toxicity_multiplier", 1.0)) + float(event.get("toxicity_shift", 0.0))) / (1.0 + float(genes.get("resilience", 0.1)) * 2.0)
	return maxf(0.0, thermal_stress - 0.5) + maxf(0.0, radiation_stress - 0.3) + maxf(0.0, pressure_stress - 0.6) + oxygen_stress * 0.35 + dehydration + toxicity

func _migration_direction(position: Vector2, genes: Dictionary, planet: Dictionary, event: Dictionary, current_stress: float, current_biome_name: String = "") -> Vector2:
	var best_stress := current_stress
	var best_direction := Vector2.ZERO
	if current_biome_name.is_empty(): current_biome_name = str(BiomeManager.at_position(position, sim_bounds, planet_index).name)
	# Neighbor samples often lie in the same biome. Their physiology is identical.
	var stress_by_biome := {current_biome_name:current_stress}
	for direction in [Vector2.LEFT, Vector2.RIGHT, Vector2.UP, Vector2.DOWN]:
		var sample: Vector2 = position + direction * 90.0
		if sample.x < sim_bounds.position.x or sample.x >= sim_bounds.end.x or sample.y < sim_bounds.position.y or sample.y >= sim_bounds.end.y:
			continue
		var candidate_biome := BiomeManager.at_position(sample, sim_bounds, planet_index)
		var name := str(candidate_biome.name)
		if not stress_by_biome.has(name): stress_by_biome[name] = _environmental_stress(genes, planet, candidate_biome, event)
		var stress := float(stress_by_biome[name])
		if stress < best_stress:
			best_stress = stress
			best_direction = direction
	return best_direction

func random_point() -> Vector2:
	return Vector2(rng.randf_range(sim_bounds.position.x + 12, sim_bounds.end.x - 12), rng.randf_range(sim_bounds.position.y + 12, sim_bounds.end.y - 12))

func wrap_position(o: Dictionary) -> void:
	var margin := 8.0
	if o.pos.x < sim_bounds.position.x + margin: o.pos.x = sim_bounds.end.x - margin
	if o.pos.x > sim_bounds.end.x - margin: o.pos.x = sim_bounds.position.x + margin
	if o.pos.y < sim_bounds.position.y + margin: o.pos.y = sim_bounds.end.y - margin
	if o.pos.y > sim_bounds.end.y - margin: o.pos.y = sim_bounds.position.y + margin

func species_counts() -> Dictionary:
	return EvolutionSystem.population_counts(organisms)

func _create_carcass(position: Vector2, reserve: float = 8.0) -> void:
	if reserve <= 0.0: return
	for i in range(food.size()):
		if food[i].x < sim_bounds.position.x:
			food[i] = position
			_set_resource_kind(i, "carcass", reserve)
			return
	if food.size() < FOOD_TARGET + 100:
		food.append(position)
		_set_resource_kind(food.size() - 1, "carcass", reserve)

func resource_kind(index: int) -> String:
	return resource_kinds[index] if index >= 0 and index < resource_kinds.size() else "nutrient"

func resource_reserve(index: int) -> float:
	return resource_reserves[index] if index >= 0 and index < resource_reserves.size() else 8.0

func _set_resource_reserve(index: int, reserve: float) -> void:
	while resource_reserves.size() <= index: resource_reserves.append(8.0)
	resource_reserves[index] = reserve

func _set_resource_kind(index: int, kind: String, reserve: float = 8.0) -> void:
	while resource_kinds.size() <= index: resource_kinds.append("nutrient")
	resource_kinds[index] = kind
	_set_resource_reserve(index, reserve)
	while _resource_biomes.size() <= index: _resource_biomes.append({})
	_resource_biomes[index] = BiomeManager.at_position(food[index], sim_bounds, planet_index)

func _kill_organism(o: Dictionary, cause: String = "unknown", harvested_energy: float = 0.0) -> void:
	if o.dead:
		return
	var remains := maxf(0.0, EcosystemSystem.carcass_reserve(o) - harvested_energy)
	o.dead = true
	o.energy = -1.0
	deaths += 1
	death_causes[cause] = int(death_causes.get(cause, 0)) + 1
	lifespan_sum += float(o.age)
	lifespan_count += 1
	_create_carcass(o.pos, remains)

func _register_species(o: Dictionary, ancestor: String, parent_genes: Dictionary = {}) -> void:
	EvolutionSystem.record_species(discovered_species, timeline, o, ancestor, elapsed, parent_genes)

func select_planet(index: int) -> void:
	planet_index = posmod(index, PlanetManager.PLANETS.size())
	_environment_dirty = true
	for organism in organisms:
		organism.environment_timer = 0.0
	timeline.append({"name":"Planet selected", "detail":PlanetManager.get_planet(planet_index).name,"time":elapsed})

func trigger_environment_event(event_name: String = "") -> void:
	var record := environment.trigger(planet_data(), elapsed, event_name)
	_environment_dirty = true
	timeline.append(record)
	for organism in organisms: organism.environment_timer = 0.0

func selected() -> Dictionary:
	for o in organisms:
		if int(o.id) == _selected_id:
			return o
	return {}

func select_at(point: Vector2) -> void:
	var best := 400.0
	_selected_id = -1
	for o in organisms:
		var distance: float = o.pos.distance_squared_to(point)
		if distance < best:
			best = distance
			_selected_id = int(o.id)


