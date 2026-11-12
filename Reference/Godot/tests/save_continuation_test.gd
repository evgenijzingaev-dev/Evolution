extends SceneTree

const Save = preload("res://scripts/save_manager.gd")
const PATH := "user://continuation_test.json"

func _initialize() -> void:
	var first := SimulationManager.new()
	first.seed_world(34, 0, 404)
	first.configure_planet(0, {"gravity":1.2,"humidity":0.9})
	for index in range(6):
		var genes: Dictionary = first.organisms[0].genes.duplicate(true)
		if index < 2:
			genes.predation = 0.9
		elif index < 4:
			genes.coloniality = 0.9
			genes.differentiation = 1.0
		else:
			genes.sexual_reproduction = 1.0
		var organism := first.make_organism(first.sim_bounds.get_center() + Vector2(index * 8, 0), genes, 1, -1)
		organism.age = 20.0
		organism.energy = 30.0
		first.organisms.append(organism)
		first._register_species(organism, "abiogenesis")
	for i in range(73): first.step(0.2)
	first._next_event_time = first.elapsed + 0.6
	first.paused = true
	assert(Save.save_simulation(first, PATH) == OK)
	var second := SimulationManager.new()
	assert(Save.load_simulation(second, PATH) == OK)
	assert(second.paused and second.rng.state == first.rng.state)
	assert(second.environment.rng.state == first.environment.rng.state)
	assert(is_equal_approx(float(second.planet_data().gravity), float(first.planet_data().gravity)))
	for tick in range(250):
		first.step(0.2)
		second.step(0.2)
		if first.rng.state != second.rng.state:
			print("Divergence tick ", tick, " populations ", first.organisms.size(), "/", second.organisms.size(), " births ",first.births,"/",second.births)
			for index in range(mini(first.organisms.size(), second.organisms.size())):
				var x: Dictionary = first.organisms[index]
				var y: Dictionary = second.organisms[index]
				if x.behavior != y.behavior or x.target_food != y.target_food:
					print("Behavior difference ",x.id," ",x.behavior,"/",y.behavior," food ",x.target_food,"/",y.target_food)
		assert(first.rng.state == second.rng.state, "Save/load must preserve the random sequence")
		assert(first.environment.rng.state == second.environment.rng.state)
		assert(first.food == second.food, "Food positions must preserve continuation")
		assert(first.resource_reserves.size() == second.resource_reserves.size())
		for resource in range(first.resource_reserves.size()):
			var reserve_tolerance := 1.0e-10 * maxf(1.0, absf(first.resource_reserves[resource]))
			assert(absf(first.resource_reserves[resource] - second.resource_reserves[resource]) <= reserve_tolerance, "Organic reserves must preserve continuation within floating-point tolerance")
		assert(first.organisms.size() == second.organisms.size() and first.births == second.births and first.deaths == second.deaths)
		assert(first.environment.current_event == second.environment.current_event)
		assert(first.statistics_history.size() == second.statistics_history.size())
		for index in range(first.organisms.size()):
			var a: Dictionary = first.organisms[index]
			var b: Dictionary = second.organisms[index]
			assert(a.id == b.id and a.pos.is_equal_approx(b.pos))
			assert(is_equal_approx(float(a.energy), float(b.energy)) and is_equal_approx(float(a.health), float(b.health)))
	DirAccess.remove_absolute(PATH)
	print("Save continuation tests passed: mixed population, 250 paired steps, RNGs, events, positions, health and statistics")
	quit(0)
