class_name GeneticsSystem
extends RefCounted

const TRAITS := ["size", "speed", "efficiency", "metabolism", "lifespan", "sensing", "temperature_tolerance", "radiation_tolerance", "pressure_tolerance", "phototrophy", "chemotrophy", "herbivory", "predation", "decomposition", "armor", "cilia", "limbs", "eyes", "luminescence", "coloniality", "gills", "max_energy", "reproduction_rate", "resilience", "flagella", "fins", "sexual_reproduction", "differentiation", "scavenging"]
const MUTATION_RATE := 0.16
const MUTATION_STEP := 0.12
const ADDITIONAL_DEFAULTS := {"max_energy":34.0,"reproduction_rate":1.0,"resilience":0.1,"flagella":0.1,"fins":0.0,"sexual_reproduction":0.0,"differentiation":0.0,"scavenging":0.0}
static var _default_genome: Dictionary = {}

static func complete_genome(partial: Dictionary) -> Dictionary:
	if _default_genome.is_empty():
		var generator := RandomNumberGenerator.new()
		generator.seed = 0
		_default_genome = starter(generator)
	var result := _default_genome.duplicate(true)
	result.merge(partial, true)
	return result

static func starter(rng: RandomNumberGenerator) -> Dictionary:
	return {"size": rng.randf_range(0.75, 1.15), "speed": rng.randf_range(24.0, 42.0), "efficiency": rng.randf_range(0.7, 1.15), "metabolism": rng.randf_range(0.55, 0.9), "lifespan": rng.randf_range(85.0, 125.0), "sensing": rng.randf_range(70.0, 125.0), "temperature_tolerance": rng.randf_range(0.7, 1.3), "radiation_tolerance": rng.randf_range(0.7, 1.3), "pressure_tolerance": rng.randf_range(0.7, 1.3), "phototrophy": rng.randf_range(0.05, 0.48), "chemotrophy": rng.randf_range(0.0, 0.18), "herbivory": rng.randf_range(0.05, 0.6), "predation": rng.randf_range(0.0, 0.08), "decomposition": rng.randf_range(0.0, 0.25), "armor": rng.randf_range(0.0, 0.2), "cilia": rng.randf_range(0.15, 0.9), "limbs": rng.randf_range(0.0, 0.1), "eyes": rng.randf_range(0.0, 0.22), "luminescence": rng.randf_range(0.0, 0.03), "coloniality": rng.randf_range(0.0, 0.08), "gills": rng.randf_range(0.0, 0.2), "max_energy":34.0,"reproduction_rate":1.0,"resilience":0.1,"flagella":0.1,"fins":0.0,"sexual_reproduction":0.0,"differentiation":0.0,"scavenging":0.0}

static func inherit(parent: Dictionary, rng: RandomNumberGenerator, rate: float = MUTATION_RATE, strength: float = MUTATION_STEP) -> Dictionary:
	return preload("res://scripts/mutation_system.gd").inherit(parent, rng, rate, strength)

static func trait_min(gene_key: String) -> float:
	return preload("res://scripts/mutation_system.gd").trait_min(gene_key)

static func trait_max(gene_key: String) -> float:
	return preload("res://scripts/mutation_system.gd").trait_max(gene_key)
static func species_signature(genes: Dictionary) -> String:
	var signature := "%d-%d-%d-%d-%d-%d-%d" % [roundi(float(genes.speed) / 9.0), roundi(float(genes.size) * 3.0), roundi(float(genes.efficiency) * 3.0), int(float(genes.predation) > 0.55), int(float(genes.coloniality) > 0.55), int(float(genes.phototrophy + genes.chemotrophy) > 0.68), int(float(genes.decomposition) > 0.55)]
	if float(genes.coloniality) >= 0.35 and float(genes.get("differentiation", 0.0)) >= 0.5: signature += "-M"
	if float(genes.predation) > 0.58 and float(genes.herbivory) > 0.68: signature += "-O"
	if float(genes.get("scavenging", 0.0)) > 0.6: signature += "-S"
	return signature

static func organism_kind(genes: Dictionary) -> String:
	if float(genes.predation) > 0.58 and float(genes.herbivory) > 0.68: return "Omnivore"
	if float(genes.predation) > 0.58:
		return "Predator"
	if float(genes.coloniality) > 0.58:
		return "Multicellular" if float(genes.get("differentiation", 0.0)) >= 0.5 else "Colony"
	if float(genes.herbivory) > 0.68:
		return "Grazer"
	if float(genes.get("scavenging", 0.0)) > 0.6 and float(genes.get("scavenging", 0.0)) > float(genes.decomposition): return "Scavenger"
	if float(genes.decomposition) > 0.58:
		return "Decomposer"
	if float(genes.phototrophy) > 0.32 or float(genes.chemotrophy) > 0.48:
		return "Producer"
	return "Microbe"

static func trophic_role(genes: Dictionary) -> String:
	# Feeding roles remain observable even when body shape is colonial.
	if float(genes.predation) > 0.58:
		return "Omnivore" if float(genes.herbivory) > 0.68 else "Predator"
	if float(genes.herbivory) > 0.68: return "Grazer"
	if float(genes.get("scavenging", 0.0)) > 0.6 and float(genes.get("scavenging", 0.0)) > float(genes.decomposition): return "Scavenger"
	if float(genes.decomposition) > 0.58: return "Decomposer"
	if float(genes.phototrophy) > 0.32 or float(genes.chemotrophy) > 0.48: return "Producer"
	return "Microbe"





