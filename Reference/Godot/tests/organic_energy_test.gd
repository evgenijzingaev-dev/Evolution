extends SceneTree

const Save = preload("res://scripts/save_manager.gd")
const PATH := "user://organic_energy_test.json"

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1,0,64)
	sim.configure_settings({"mutation_rate":0.0,"resource_regrowth":0.0})
	sim.food.clear()
	sim.resource_kinds.clear()
	sim.resource_reserves.clear()
	sim._next_event_time = 10000.0
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	genes.phototrophy = 0.0
	genes.chemotrophy = 0.0
	genes.predation = 0.0
	genes.herbivory = 0.0
	genes.scavenging = 1.0
	genes.decomposition = 1.0
	genes.efficiency = 2.2
	genes.speed = 0.0
	genes.gills = 1.0
	genes.lifespan = 4.0
	genes.reproduction_rate = 1000.0
	genes.sexual_reproduction = 0.0
	sim.organisms.clear()
	for index in range(3):
		var o := sim.make_organism(sim.sim_bounds.get_center(),genes.duplicate(true),0,-1)
		o.energy = 30.0
		sim.organisms.append(o)
	var previous := _total_energy(sim)
	for tick in range(600):
		sim.step(0.2)
		var total := _total_energy(sim)
		assert(total <= previous + 0.000001, "Birth/death/scavenging cycles cannot create energy without an external source")
		previous = total
		if sim.organisms.is_empty(): break
	assert(sim.births > 0 and sim.deaths > 0 and sim.organisms.is_empty(), "Recycling alone must not sustain an indefinitely reproducing population")
	assert(EcosystemSystem.resource_energy(genes,"carcass",2.0,10.0) <= 1.8, "Efficiency and tissues cannot multiply a carcass beyond its reserve")
	# The immediate kill intake and the remaining carcass share one prey budget.
	sim.seed_world(1,0,64)
	sim.food.clear()
	sim.resource_kinds.clear()
	sim.resource_reserves.clear()
	sim.settings.resource_regrowth = 0.0
	var prey := sim.make_organism(sim.sim_bounds.get_center(),genes.duplicate(true),0,-1)
	prey.energy = 2.0
	prey.health = 0.1
	var hunter_genes := genes.duplicate(true)
	hunter_genes.predation = 1.0
	hunter_genes.reproduction_rate = 0.0
	var hunter := sim.make_organism(prey.pos,hunter_genes,0,-1)
	hunter.energy = 10.0
	hunter.hunt_timer = 0.0
	sim.organisms.assign([prey,hunter])
	previous = _total_energy(sim)
	sim.step(0.2)
	assert(prey.dead and hunter.energy > 10.0)
	assert(_total_energy(sim) <= previous, "Predation must debit the same reserve subsequently available to scavengers")
	sim._create_carcass(hunter.pos + Vector2(50,0),3.25)
	assert(Save.save_simulation(sim,PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored,PATH) == OK)
	assert(restored.resource_reserves == sim.resource_reserves)
	var payload: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PATH))
	payload.erase("resource_reserves")
	payload.version = 7
	var file := FileAccess.open(PATH,FileAccess.WRITE)
	file.store_string(JSON.stringify(payload,"",true,true))
	file.close()
	assert(Save.load_simulation(restored,PATH) == OK and restored.resource_reserve(0) == 8.0)
	DirAccess.remove_absolute(PATH)
	print("Organic energy tests passed: closed birth/death/scavenging cycles, finite assimilation, shared predation budget, reserve persistence and v7 migration")
	quit(0)

func _total_energy(sim: SimulationManager) -> float:
	var total := 0.0
	for o in sim.organisms:
		if not o.dead: total += maxf(0.0,float(o.energy)) + OrganismManager.structural_energy(o.body)
	for index in range(sim.food.size()):
		if sim.food[index].x >= sim.sim_bounds.position.x and sim.resource_kind(index) == "carcass": total += sim.resource_reserve(index)
	return total
