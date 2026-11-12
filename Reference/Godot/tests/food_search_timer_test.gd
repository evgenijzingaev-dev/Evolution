extends SceneTree

func _initialize() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1,0,91)
	sim.settings.resource_regrowth = 0.0
	sim.food.clear()
	sim.resource_kinds.clear()
	sim.resource_reserves.clear()
	var organism: Dictionary = sim.organisms[0]
	organism.pos = sim.sim_bounds.get_center()
	organism.genes.speed = 0.0
	organism.genes.gills = 1.0
	sim.step(0.2)
	assert(organism.target_food == -1 and organism.think_timer > 0.0)
	var previous_timer := float(organism.think_timer)
	sim.food.append(organism.pos + Vector2(40,0))
	sim.resource_kinds.append("nutrient")
	sim.resource_reserves.append(8.0)
	sim.step(0.2)
	assert(organism.target_food == -1 and organism.think_timer < previous_timer, "An empty search must wait for its next scheduled update")
	for tick in range(10): sim.step(0.2)
	assert(organism.target_food == 0, "A waiting organism must eventually discover newly available food")
	EcosystemSystem.consume(sim.food,0)
	sim.step(0.2)
	assert(organism.target_food == -1, "A consumed target must be invalidated immediately")
	previous_timer = float(organism.think_timer)
	sim.step(0.2)
	assert(organism.think_timer < previous_timer, "Lost targets must not restart a full search on every tick")
	var fresh := sim.make_organism(organism.pos,organism.genes.duplicate(true),0,-1)
	sim.food[0] = fresh.pos + Vector2(40,0)
	sim.organisms.assign([fresh])
	sim.step(0.2)
	assert(fresh.target_food == 0, "Newborns and founders must still search on their first update")
	print("Food search timer tests passed: empty-search backoff, delayed discovery, immediate invalidation, depletion backoff and initial acquisition")
	quit(0)
