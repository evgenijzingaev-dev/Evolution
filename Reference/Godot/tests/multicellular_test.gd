extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 19)
	const Body = preload("res://scripts/multicellular_system.gd")
	var genes: Dictionary = sim.organisms[0].genes.duplicate(true)
	var single := Body.develop(genes)
	assert(single.cell_count == 1 and single.upkeep == 0.0)
	genes.coloniality = 1.0
	genes.armor = 1.0
	genes.resilience = 1.0
	genes.cilia = 1.0
	genes.phototrophy = 1.0
	var general := Body.develop(genes)
	genes.differentiation = 1.0
	var specialized := Body.develop(genes)
	assert(specialized.cell_count == 9 and specialized.roles.size() == 9)
	assert(specialized.feeding_cells > 0 and specialized.motor_cells > 0 and specialized.defense_cells > 0)
	assert(specialized.feeding_multiplier > general.feeding_multiplier)
	assert(specialized.movement_multiplier > general.movement_multiplier)
	assert(specialized.health_multiplier > general.health_multiplier and specialized.damage_multiplier < general.damage_multiplier)
	assert(specialized.upkeep > general.upkeep, "Specialists must have a maintenance cost")
	assert(GeneticsSystem.organism_kind(genes) == "Multicellular")
	var organism := sim.make_organism(sim.sim_bounds.get_center(), genes, 1, -1)
	sim.organisms.clear()
	sim.organisms.append(organism)
	assert(organism.body == specialized)
	var child_genes := GeneticsSystem.inherit(genes, sim.rng, 0.0)
	assert(Body.develop(child_genes) == specialized, "Body plan must be inherited through genes")
	const Save = preload("res://scripts/save_manager.gd")
	const PATH := "user://multicellular_test.json"
	assert(Save.save_simulation(sim, PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, PATH) == OK and restored.organisms[0].body == specialized)
	DirAccess.remove_absolute(PATH)
	print("Multicellular tests passed: cell roles, functional specialization, upkeep, inherited body, restored body")
	quit(0)
