class_name EcosystemSystem
extends RefCounted

static func grid_cell(position: Vector2, bounds: Rect2, cells: Vector2i) -> Vector2i:
	return Vector2i(clampi(int((position.x - bounds.position.x) / bounds.size.x * cells.x), 0, cells.x - 1), clampi(int((position.y - bounds.position.y) / bounds.size.y * cells.y), 0, cells.y - 1))

static func build_nutrient_grid(food: Array[Vector2], bounds: Rect2, cells: Vector2i, max_per_cell: int, kinds: Array[String] = []) -> Dictionary:
	var grid := {}
	for i in range(food.size()):
		if food[i].x < bounds.position.x:
			continue
		var cell := grid_cell(food[i], bounds, cells)
		if not grid.has(cell):
			grid[cell] = [i]
			continue
		var cell_food: Array = grid[cell]
		if cell_food.size() < max_per_cell:
			cell_food.append(i)
		else:
			var cell_center := bounds.position + Vector2((float(cell.x) + 0.5) * bounds.size.x / cells.x, (float(cell.y) + 0.5) * bounds.size.y / cells.y)
			var farthest_slot := 0
			var farthest_distance := -1.0
			var kind := kinds[i] if i < kinds.size() else "nutrient"
			var matching: Array[int] = []
			for slot in range(cell_food.size()):
				var existing_index := int(cell_food[slot])
				if (kinds[existing_index] if existing_index < kinds.size() else "nutrient") == kind: matching.append(slot)
			for slot in range(cell_food.size()):
				if not matching.is_empty() and slot not in matching: continue
				var distance: float = cell_center.distance_squared_to(food[cell_food[slot]])
				if distance > farthest_distance:
					farthest_distance = distance
					farthest_slot = slot
			if matching.is_empty() or cell_center.distance_squared_to(food[i]) < farthest_distance:
				cell_food[farthest_slot] = i
	return grid

static func carcass_reserve(organism: Dictionary) -> float:
	return (maxf(0.0, float(organism.energy)) + OrganismManager.structural_energy(organism.body)) * 0.75

static func resource_energy(genes: Dictionary, kind: String, reserve: float = 8.0, feeding_multiplier: float = 1.0) -> float:
	var efficiency := float(genes.efficiency)
	if kind == "carcass":
		var assimilation := 0.15 + float(genes.decomposition) * 0.35 + float(genes.get("scavenging", 0.0)) * 0.35 + float(genes.predation) * 0.1
		return maxf(0.0, reserve) * clampf(assimilation * efficiency * feeding_multiplier, 0.0, 0.9)
	var strongest_trait := maxf(maxf(float(genes.predation), float(genes.herbivory)), maxf(float(genes.get("scavenging", 0.0)), float(genes.decomposition)))
	# Primitive cells keep their basal nutrient diet; strong specialization trades it off.
	var specialization := clampf((strongest_trait - 0.6) / 0.4, 0.0, 1.0)
	return 8.0 * efficiency * (1.0 - minf(0.75, specialization * 0.65)) * feeding_multiplier

static func resource_preference(genes: Dictionary, kind: String) -> float:
	if kind == "carcass": return 1.0 + 2.0 * maxf(float(genes.decomposition), float(genes.get("scavenging", 0.0)))
	return 1.0

static func nearest_nutrient(food: Array[Vector2], grid: Dictionary, position: Vector2, cell: Vector2i, radius: float, cell_radius: int, cells: Vector2i) -> Dictionary:
	var nearest_index := -1
	var nearest_distance := radius * radius
	for x in range(maxi(0, cell.x - cell_radius), mini(cells.x, cell.x + cell_radius + 1)):
		for y in range(maxi(0, cell.y - cell_radius), mini(cells.y, cell.y + cell_radius + 1)):
			for candidate in grid.get(Vector2i(x, y), []):
				var distance: float = position.distance_squared_to(food[candidate])
				if distance < nearest_distance:
					nearest_distance = distance
					nearest_index = candidate
	return {"index": nearest_index, "distance_squared": nearest_distance}

static func consume(food: Array[Vector2], index: int) -> bool:
	if index < 0 or index >= food.size() or food[index].x < 0.0:
		return false
	food[index] = Vector2(-1000, -1000)
	return true
