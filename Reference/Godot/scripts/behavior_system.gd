class_name BehaviorSystem
extends RefCounted

static func nearby_threat(organism: Dictionary, grid: Dictionary, cell: Vector2i, cells: Vector2i, radius: float) -> Dictionary:
	var nearest := radius * radius
	var result := {}
	for x in range(maxi(0, cell.x - 1), mini(cells.x, cell.x + 2)):
		for y in range(maxi(0, cell.y - 1), mini(cells.y, cell.y + 2)):
			for candidate in grid.get(Vector2i(x, y), []):
				if candidate.dead or candidate.id == organism.id or candidate.species == organism.species:
					continue
				if float(candidate.genes.predation) <= 0.55 or float(candidate.genes.size) * 1.25 < float(organism.genes.size):
					continue
				var distance: float = organism.pos.distance_squared_to(candidate.pos)
				if distance < nearest:
					return {"id":candidate.id,"pos":candidate.pos}
	return result

static func choose_state(has_threat: bool, has_prey: bool, has_food: bool, energy: float, predator: bool) -> String:
	if has_threat and not predator:
		return "FLEEING"
	if has_prey:
		return "HUNTING" if predator else "GRAZING"
	if has_food:
		return "FORAGING"
	return "EXPLORING" if energy < 16.0 else "RESTING"

static func movement_speed(genes: Dictionary, energy: float, body: Dictionary = {}) -> float:
	var developed := preload("res://scripts/multicellular_system.gd").develop(genes) if body.is_empty() else body
	return float(genes.speed) * (0.62 + minf(0.5, energy / 55.0)) * (0.7 + float(genes.cilia) * 0.25 + float(genes.limbs) * 0.35 + float(genes.get("flagella", 0.1)) * 0.25 + float(genes.get("fins", 0.0)) * 0.3) * float(developed.movement_multiplier)

static func metabolism_drain(genes: Dictionary, stress: float, state: String) -> float:
	var resting_discount := 0.72 if state == "RESTING" else 1.0
	var armor_resistance := float(genes.armor) * 0.035
	return maxf(0.01, 0.10 + float(genes.metabolism) * 0.075 + float(genes.size) * 0.025 + stress * 0.09 - armor_resistance) * resting_discount

static func reproduction_roll(rng: RandomNumberGenerator, energy: float, population: int, max_population: int, dt: float, efficiency: float, reproduction_rate: float = 1.0) -> bool:
	return energy > 23.0 and population < max_population and rng.randf() < 0.035 * dt * (0.5 + efficiency) * reproduction_rate
