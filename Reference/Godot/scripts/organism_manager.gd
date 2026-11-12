class_name OrganismManager
extends RefCounted

const Multicellular = preload("res://scripts/multicellular_system.gd")

static func structural_energy(body: Dictionary) -> float:
	return 1.0 + float(body.get("cell_count", 1) - 1) * 0.5

static func create(id: int, position: Vector2, genes: Dictionary, generation: int, parent_id: int, rng: RandomNumberGenerator) -> Dictionary:
	for gene_key in GeneticsSystem.ADDITIONAL_DEFAULTS:
		if not genes.has(gene_key):
			genes[gene_key] = GeneticsSystem.ADDITIONAL_DEFAULTS[gene_key]
	return {"id":id,"pos":position,"heading":rng.randf_range(-PI,PI),"genes":genes,"kind":GeneticsSystem.organism_kind(genes),"behavior":"FORAGING","energy":18.0,"age":0.0,"generation":generation,"parent":parent_id,"species":GeneticsSystem.species_signature(genes),"body":Multicellular.develop(genes),"health":max_health(genes),"dead":false,"target_prey":-1,"hunt_timer":float(id % 4) * 0.2,"target_food":-1,"think_timer":rng.randf_range(0.0,1.5),"environment_timer":0.0,"biome_cache":{},"environmental_stress":0.0,"migration_direction":Vector2.ZERO}

static func max_health(genes: Dictionary, body: Dictionary = {}) -> float:
	var developed := Multicellular.develop(genes) if body.is_empty() else body
	return (10.0 + float(genes.size) * 4.0 + float(genes.get("resilience", 0.1)) * 8.0) * float(developed.health_multiplier)

static func damage(organism: Dictionary, raw_damage: float) -> bool:
	var genes: Dictionary = organism.genes
	var reduction := clampf(float(genes.armor) * 0.45 + float(genes.get("resilience", 0.1)) * 0.3, 0.0, 0.8)
	var body: Dictionary = organism.get("body", {})
	organism.health = maxf(0.0, float(organism.get("health", max_health(genes, body))) - maxf(0.0, raw_damage) * (1.0 - reduction) * float(body.get("damage_multiplier", 1.0)))
	return organism.health <= 0.0

static func recover(organism: Dictionary, dt: float) -> void:
	var maximum := max_health(organism.genes, organism.get("body", {}))
	organism.health = minf(maximum, float(organism.get("health", maximum)) + (0.12 * dt if float(organism.energy) > 16.0 else 0.0))



