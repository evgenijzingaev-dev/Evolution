extends SceneTree

const Reproduction = preload("res://scripts/reproduction_system.gd")

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 53)
	sim.configure_settings({"mutation_rate":0.0,"resource_regrowth":0.0})
	sim.food.clear()
	sim.resource_kinds.clear()
	var parent: Dictionary = sim.organisms[0]
	parent.genes.phototrophy = 0.0
	parent.genes.chemotrophy = 0.0
	parent.genes.predation = 0.0
	parent.genes.herbivory = 0.0
	parent.genes.speed = 0.0
	parent.genes.reproduction_rate = 1000.0
	parent.genes.lifespan = 10000.0
	parent.genes.max_energy = 100.0
	parent.energy = 100.0
	parent.pos = sim.sim_bounds.get_center()
	var previous_total := 100.0
	for tick in range(30):
		sim.step(0.2)
		var total := _energy(sim)
		assert(total <= previous_total, "Repeated division in a closed, food-free system must not create stored energy")
		previous_total = total
	assert(sim.births > 1 and sim.organisms.size() > 1)
	# A genetic capacity change limits the reserve transferred to the child.
	var child_genes: Dictionary = parent.genes.duplicate(true)
	child_genes.max_energy = 16.0
	var child := sim.make_organism(parent.pos, child_genes, 1, parent.id)
	parent.energy = 30.0
	assert(Reproduction.provision_offspring(parent, child))
	assert(child.energy == 8.0 and is_equal_approx(float(parent.energy + child.energy), 29.0))
	var complex_genes := child_genes.duplicate(true)
	complex_genes.coloniality = 1.0
	complex_genes.differentiation = 1.0
	var complex_child := sim.make_organism(parent.pos, complex_genes, 1, parent.id)
	parent.energy = 30.0
	assert(Reproduction.provision_offspring(parent, complex_child))
	assert(parent.energy + complex_child.energy < 29.0, "Building additional body cells must consume energy")
	# Both parents fund sexual offspring and both remain alive.
	var mate := sim.make_organism(parent.pos, parent.genes.duplicate(true), 0, -1)
	parent.energy = 30.0
	mate.energy = 26.0
	assert(Reproduction.provision_offspring(parent, child, mate))
	assert(parent.energy < 30.0 and mate.energy < 26.0)
	assert(is_equal_approx(float(parent.energy + mate.energy + child.energy), 55.0))
	var before := [parent.energy,mate.energy,child.energy]
	parent.energy = 0.1
	before[0] = parent.energy
	assert(not Reproduction.provision_offspring(parent, child, mate))
	assert(before == [parent.energy,mate.energy,child.energy], "An unfunded birth must leave all reserves untouched")
	# Check the actual paired-birth path, including cooldown and the newborn.
	sim.organisms.clear()
	for adult in [parent,mate]:
		adult.energy = 30.0
		adult.age = 20.0
		adult.genes.sexual_reproduction = 1.0
		adult.species = "energy-pair"
		sim.organisms.append(adult)
	sim._organism_grid_timer = 0.0
	var births_before := sim.births
	sim.step(0.2)
	assert(sim.births == births_before + 1 and _energy(sim) < 60.0)
	print("Birth energy tests passed: repeated closed-system division, reserve capacity, tissue costs, paired funding, failed transaction and actual sexual birth")
	quit(0)

func _energy(sim: SimulationManager) -> float:
	var total := 0.0
	for organism in sim.organisms: total += float(organism.energy)
	return total
