extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 64)
	var organism: Dictionary = sim.organisms[0]
	for key in GeneticsSystem.ADDITIONAL_DEFAULTS:
		assert(organism.genes.has(key), "New genes must be present in founders")
	var defended: Dictionary = organism.duplicate(true)
	defended.genes.armor = 1.0
	defended.genes.resilience = 1.0
	var initial_health := float(organism.health)
	var defended_health := float(defended.health)
	OrganismManager.damage(organism, 5.0)
	OrganismManager.damage(defended, 5.0)
	assert(initial_health - float(organism.health) > defended_health - float(defended.health), "Defense genes must reduce damage")
	var before_recovery := float(organism.health)
	OrganismManager.recover(organism, 1.0)
	assert(float(organism.health) > before_recovery, "Fed organisms recover health")
	var speed := BehaviorSystem.movement_speed(organism.genes, 18.0)
	var swimming_genes: Dictionary = organism.genes.duplicate(true)
	swimming_genes.fins = 1.0
	swimming_genes.flagella = 1.0
	assert(BehaviorSystem.movement_speed(swimming_genes, 18.0) > speed, "Locomotion genes must increase movement")
	assert(not BehaviorSystem.reproduction_roll(sim.rng, 30.0, 1, 100, 1.0, 1.0, 0.0), "Zero reproduction rate must suppress births")
	organism.genes.max_energy = 20.0
	organism.energy = 30.0
	sim.step(0.2)
	assert(float(sim.organisms[0].energy) <= 20.0, "Genetic energy capacity must limit storage")
	const Save = preload("res://scripts/save_manager.gd")
	const PATH := "user://physiology_test.json"
	assert(Save.save_simulation(sim, PATH) == OK)
	var restored := SimulationManager.new()
	assert(Save.load_simulation(restored, PATH) == OK)
	assert(is_equal_approx(float(restored.organisms[0].health), float(sim.organisms[0].health)), "Health must survive save/load")
	DirAccess.remove_absolute(PATH)
	print("Physiology tests passed: defense, recovery, locomotion, reproduction, energy capacity, saved health")
	quit(0)
