class_name EvolutionSystem
extends RefCounted

static func record_species(discovered: Dictionary, history: Array[Dictionary], organism: Dictionary, ancestor: String, elapsed: float, parent_genes: Dictionary = {}) -> bool:
	var signature: String = organism.species
	if discovered.has(signature):
		return false
	var changes: Array[Dictionary] = []
	for gene_key in parent_genes:
		if organism.genes.has(gene_key) and absf(float(organism.genes[gene_key]) - float(parent_genes[gene_key])) > 0.001:
			changes.append({"gene":gene_key,"from":parent_genes[gene_key],"to":organism.genes[gene_key]})
	discovered[signature] = {"signature":signature,"ancestor":ancestor,"first_seen":elapsed,"generation":organism.generation,"population":0,"extinct_at":-1.0,"genes":organism.genes.duplicate(true),"kind":organism.kind,"changes":changes}
	history.append({"name":"New species detected", "detail":signature,"time":elapsed,"ancestor":ancestor})
	return true

static func population_counts(organisms: Array[Dictionary]) -> Dictionary:
	var result := {}
	for organism in organisms:
		result[organism.species] = int(result.get(organism.species, 0)) + 1
	return result
