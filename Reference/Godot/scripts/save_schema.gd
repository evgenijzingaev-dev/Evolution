extends RefCounted

const UNIT_GENES := ["phototrophy","chemotrophy","herbivory","predation","decomposition","armor","cilia","limbs","eyes","luminescence","coloniality","gills","resilience","flagella","fins","sexual_reproduction","differentiation","scavenging"]

static func number(value: Variant) -> bool:
	return (value is float or value is int) and is_finite(float(value))

static func integer(value: Variant, minimum: float = 0.0) -> bool:
	return number(value) and float(value) >= minimum and float(value) <= 1000000000000.0 and floor(float(value)) == float(value)

static func genome(value: Variant) -> bool:
	if not value is Dictionary: return false
	for key in value:
		if not key is String or not number(value[key]) or absf(float(value[key])) > 1000000.0: return false
		if key in UNIT_GENES and (float(value[key]) < 0.0 or float(value[key]) > 1.0): return false
		if key in ["size","lifespan","temperature_tolerance","radiation_tolerance","pressure_tolerance","max_energy"] and float(value[key]) <= 0.0: return false
		if key in ["speed","sensing","efficiency","metabolism","reproduction_rate"] and float(value[key]) < 0.0: return false
	return true

static func validate(data: Dictionary) -> bool:
	if not integer(data.get("version"), 1.0): return false
	if data.has("resource_reserves"):
		if not data.get("food") is Array or not data.resource_reserves is Array or data.resource_reserves.size() != data.food.size(): return false
		for reserve in data.resource_reserves:
			if not number(reserve) or float(reserve) < 0.0 or float(reserve) > 1000000.0: return false
	if data.has("resource_kinds"):
		if not data.get("food") is Array or not data.resource_kinds is Array or data.resource_kinds.size() != data.food.size(): return false
		for kind in data.resource_kinds:
			if not kind is String or kind not in ["nutrient", "carcass"]: return false
	for key in ["births","deaths","mutations","generation_sum","lifespan_count","next_id","speed_index","planet_index"]:
		if data.has(key) and not integer(data[key]): return false
	for key in ["elapsed","lifespan_sum"]:
		if data.has(key) and (not number(data[key]) or float(data[key]) < 0.0 or float(data[key]) > 1000000000000000.0): return false
	if not data.get("organisms") is Array: return false
	for item in data.organisms:
		if not item is Dictionary or not genome(item.get("genes")): return false
		for key in ["id","generation"]:
			if not integer(item.get(key)): return false
		for key in ["parent","second_parent","target_food","target_prey"]:
			if item.has(key) and not integer(item[key], -1.0): return false
		for key in ["species","behavior"]:
			if item.has(key) and not item[key] is String: return false
		for key in ["age","health","think_timer","environment_timer","environmental_stress","reproduction_cooldown","hunt_timer","threat_timer"]:
			if item.has(key) and (not number(item[key]) or float(item[key]) < 0.0): return false
	for key in ["timeline","events","statistics"]:
		if not data.get(key, []) is Array or data.get(key, []).size() > 100000: return false
		for record in data.get(key, []):
			if not record is Dictionary: return false
			for field in ["time","duration","mean_energy"]:
				if record.has(field) and not number(record[field]): return false
			for field in ["name","detail","ancestor"]:
				if record.has(field) and not record[field] is String: return false
			if key == "statistics" and not integer(record.get("population")): return false
			for field in ["population","species","producers","grazers","predators","decomposers","omnivores","scavengers"]:
				if record.has(field) and not integer(record[field]): return false
	if not data.get("species", {}) is Dictionary or data.get("species", {}).size() > 100000: return false
	for record in data.get("species", {}).values():
		if not record is Dictionary or not genome(record.get("genes", {})): return false
		for key in ["signature","ancestor","kind"]:
			if record.has(key) and not record[key] is String: return false
		for key in ["generation","population"]:
			if not integer(record.get(key)): return false
		for key in ["first_seen","extinct_at"]:
			if not number(record.get(key)): return false
		if not record.get("changes", []) is Array or record.get("changes", []).size() > 1000: return false
		for change in record.get("changes", []):
			if not change is Dictionary or not change.get("gene") is String or not number(change.get("from")) or not number(change.get("to")): return false
	if not data.get("active_event", {}) is Dictionary: return false
	if data.get("active_event", {}).has("name") and not data.active_event.name is String: return false
	return true

