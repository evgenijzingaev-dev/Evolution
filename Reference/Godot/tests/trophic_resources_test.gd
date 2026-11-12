extends SceneTree

const Save = preload("res://scripts/save_manager.gd")
const PATH := "user://trophic_test.json"

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 91)
	sim.settings.resource_regrowth = 0.0
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	genes.speed = 0.0
	genes.predation = 0.0
	genes.herbivory = 0.0
	genes.decomposition = 0.0
	genes.scavenging = 0.0
	genes.gills = 1.0
	genes.temperature_tolerance = 2.0
	genes.radiation_tolerance = 2.0
	genes.pressure_tolerance = 2.0
	var scavenger_genes := genes.duplicate(true)
	scavenger_genes.scavenging = 1.0
	var decomposer_genes := genes.duplicate(true)
	decomposer_genes.decomposition = 1.0
	assert(EcosystemSystem.resource_energy(scavenger_genes, "carcass") > EcosystemSystem.resource_energy(genes, "carcass"))
	assert(EcosystemSystem.resource_energy(decomposer_genes, "carcass") > EcosystemSystem.resource_energy(genes, "carcass"))
	assert(EcosystemSystem.resource_energy(scavenger_genes, "nutrient") < EcosystemSystem.resource_energy(genes, "nutrient"), "Organic specialization must have a mineral-feeding tradeoff")
	# Dense buckets retain both food classes rather than hiding every carcass.
	var food: Array[Vector2] = [Vector2(50,50), Vector2(51,50), Vector2(90,90), Vector2(52,50)]
	var kinds: Array[String] = ["nutrient", "nutrient", "carcass", "nutrient"]
	var grid := EcosystemSystem.build_nutrient_grid(food, Rect2(0,0,100,100), Vector2i.ONE, 2, kinds)
	assert(2 in grid[Vector2i.ZERO] and grid[Vector2i.ZERO].size() == 2)
	sim.food.clear()
	sim.resource_kinds.clear()
	var scavenger := sim.make_organism(sim.sim_bounds.get_center(), scavenger_genes, 0, -1)
	scavenger.energy = 10.0
	sim.organisms = [scavenger]
	sim._create_carcass(scavenger.pos)
	assert(sim.resource_kind(0) == "carcass")
	sim.step(0.2)
	assert(scavenger.energy > 10.0 and sim.food[0].x < 0.0, "Scavengers must actually gain energy by consuming remains")
	assert(scavenger.behavior == "SCAVENGING")
	sim._create_carcass(scavenger.pos + Vector2(60,0))
	sim.food.append(scavenger.pos + Vector2(45,0))
	sim.resource_kinds.append("nutrient")
	for tick in range(10): sim.step(0.2)
	assert(scavenger.target_food == 0, "Scavengers should prefer useful remains over slightly closer minerals")
	sim._record_statistics()
	assert(sim.statistics_history.back().scavengers == 1)
	assert(Save.save_simulation(sim, PATH) == OK)
	var loaded := SimulationManager.new()
	assert(Save.load_simulation(loaded, PATH) == OK)
	assert(loaded.resource_kinds == sim.resource_kinds and loaded.organisms[0].genes.scavenging == 1.0)
	var payload: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PATH))
	var malformed := payload.duplicate(true)
	malformed.resource_kinds[0] = "unknown"
	var invalid_file := FileAccess.open(PATH, FileAccess.WRITE)
	invalid_file.store_string(JSON.stringify(malformed, "", true, true))
	invalid_file.close()
	assert(Save.load_simulation(loaded, PATH) == ERR_INVALID_DATA and loaded.resource_kind(0) == "carcass")
	payload.erase("resource_kinds")
	payload.version = 6
	var file := FileAccess.open(PATH, FileAccess.WRITE)
	file.store_string(JSON.stringify(payload, "", true, true))
	file.close()
	assert(Save.load_simulation(loaded, PATH) == OK and loaded.resource_kind(0) == "nutrient", "Older saves must receive deterministic mineral defaults")
	# Omnivores can hunt consumers, while strict grazers cannot.
	var omnivore_genes := genes.duplicate(true)
	omnivore_genes.predation = 1.0
	omnivore_genes.herbivory = 1.0
	assert(GeneticsSystem.organism_kind(omnivore_genes) == "Omnivore")
	var grazer_genes := genes.duplicate(true)
	grazer_genes.herbivory = 1.0
	var grazer := sim.make_organism(scavenger.pos, grazer_genes, 0, -1)
	grazer.hunt_timer = 0.0
	grazer.energy = 10.0
	sim.organisms.append(grazer)
	sim.step(0.2)
	assert(not scavenger.dead and grazer.behavior != "GRAZING", "Strict grazers must not attack scavengers")
	sim.organisms.erase(grazer)
	var hunter := sim.make_organism(scavenger.pos, omnivore_genes, 0, -1)
	hunter.hunt_timer = 0.0
	hunter.energy = 10.0
	scavenger.health = 0.1
	sim.organisms.append(hunter)
	sim.step(0.2)
	assert(scavenger.dead and hunter.behavior == "HUNTING", "Omnivores must hunt non-producer prey")
	sim._record_statistics()
	assert(sim.statistics_history.back().omnivores == 1)
	DirAccess.remove_absolute(PATH)
	print("Trophic resources tests passed: organic yields, specialization tradeoff, mixed buckets, scavenging, omnivory, statistics and save migration")
	quit(0)
