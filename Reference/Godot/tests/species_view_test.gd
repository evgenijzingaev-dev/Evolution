extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1, 0, 101)
	var founder: Dictionary = sim.organisms[0]
	var genes: Dictionary = founder.genes.duplicate(true)
	genes.speed = 90.0
	var child := sim.make_organism(founder.pos, genes, 1, int(founder.id))
	sim._register_species(child, str(founder.species), founder.genes)
	sim.organisms.append(child)
	var view := preload("res://scripts/species_view.gd").new()
	root.add_child(view)
	view.configure(sim, str(child.species))
	assert(view.items[child.species].get_parent() == view.items[founder.species], "Child branch must attach to its ancestor")
	assert(view.details.text.to_lower().contains("скорость:") and view.details.text.contains("90.000"), "Selected branch must show its mutation and genome")
	var old_record: Dictionary = sim.discovered_species[founder.species]
	old_record.erase("genes")
	old_record.erase("changes")
	var older_view := preload("res://scripts/species_view.gd").new()
	root.add_child(older_view)
	older_view.configure(sim, str(founder.species))
	assert(older_view.details.text.contains("старом сохранении"), "Old saves must remain browsable")
	var window := preload("res://scripts/ui_manager.gd").show_genome(root, child)
	assert(window.get_child(0).text.contains("90.000"), "Live genome must use selected organism data")
	window.queue_free()
	view.queue_free()
	older_view.queue_free()
	print("Species view tests passed: parent branches, selection, mutations, legacy records, genome inspector")
	quit(0)

