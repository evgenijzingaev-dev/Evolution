extends RefCounted

const RESERVE_FRACTION := 0.45

static func provision_offspring(parent: Dictionary, child: Dictionary, mate: Dictionary = {}) -> bool:
	var reserve := minf(float(parent.energy) * RESERVE_FRACTION, float(child.genes.max_energy) * 0.5)
	if not mate.is_empty(): reserve = minf(reserve, float(mate.energy) * RESERVE_FRACTION)
	var overhead := OrganismManager.structural_energy(child.body)
	var share := (reserve + overhead) / (1.0 if mate.is_empty() else 2.0)
	if reserve <= 0.0 or float(parent.energy) <= share or (not mate.is_empty() and float(mate.energy) <= share):
		return false
	# Every unit in the newborn's reserve is debited from its parents.
	parent.energy -= share
	if not mate.is_empty(): mate.energy -= share
	child.energy = reserve
	return true

static func eligible(organism: Dictionary) -> bool:
	return not organism.dead and float(organism.get("health", 0.0)) > 0.0 and float(organism.energy) > 23.0 and float(organism.age) >= 10.0 and float(organism.get("reproduction_cooldown", 0.0)) <= 0.0

static func find_mate(organism: Dictionary, grid: Dictionary, cell: Vector2i, cells: Vector2i, radius: float) -> Dictionary:
	var nearest := radius * radius
	var result := {}
	for x in range(maxi(0, cell.x - 2), mini(cells.x, cell.x + 3)):
		for y in range(maxi(0, cell.y - 2), mini(cells.y, cell.y + 3)):
			for candidate in grid.get(Vector2i(x, y), []):
				if candidate.id == organism.id or candidate.species != organism.species or float(candidate.genes.get("sexual_reproduction", 0.0)) < 0.6 or not eligible(candidate):
					continue
				var distance: float = organism.pos.distance_squared_to(candidate.pos)
				if distance < nearest:
					nearest = distance
					result = candidate
	return result

static func recombine(first: Dictionary, second: Dictionary, rng: RandomNumberGenerator) -> Dictionary:
	var genome := first.duplicate(true)
	for key in genome:
		if second.has(key) and rng.randf() < 0.5:
			genome[key] = second[key]
	return genome
