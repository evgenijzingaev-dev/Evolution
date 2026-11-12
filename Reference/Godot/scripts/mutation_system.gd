extends RefCounted

static func inherit(parent: Dictionary, rng: RandomNumberGenerator, rate: float = 0.16, strength: float = 0.12) -> Dictionary:
	var child := parent.duplicate(true)
	for gene_key in parent:
		if rng.randf() < rate:
			if gene_key in ["phototrophy", "chemotrophy", "herbivory", "predation", "decomposition", "armor", "cilia", "limbs", "eyes", "luminescence", "coloniality", "gills", "resilience", "flagella", "fins", "sexual_reproduction", "differentiation", "scavenging"]:
				child[gene_key] = clampf(float(child[gene_key]) + rng.randfn(0.0, strength * (0.10 / 0.12)), 0.0, 1.0)
			else:
				child[gene_key] = clampf(float(child[gene_key]) * (1.0 + rng.randfn(0.0, strength)), trait_min(gene_key), trait_max(gene_key))
	return child

static func trait_min(gene_key: String) -> float:
	if gene_key == "max_energy": return 16.0
	if gene_key == "reproduction_rate": return 0.1
	return 8.0 if gene_key == "speed" or gene_key == "sensing" else (30.0 if gene_key == "lifespan" else 0.25)

static func trait_max(gene_key: String) -> float:
	if gene_key == "max_energy": return 100.0
	if gene_key == "reproduction_rate": return 3.0
	return 90.0 if gene_key == "speed" else (220.0 if gene_key == "sensing" else (240.0 if gene_key == "lifespan" else 2.2))





