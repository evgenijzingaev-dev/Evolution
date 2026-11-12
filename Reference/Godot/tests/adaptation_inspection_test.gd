extends SceneTree

const Summary = preload("res://scripts/adaptation_summary.gd")
const UI = preload("res://scripts/ui_manager.gd")
const RU = preload("res://scripts/localization_ru.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim := SimulationManager.new()
	sim.seed_world(1,0,101)
	var founder: Dictionary = sim.organisms[0]
	var genes: Dictionary = founder.genes.duplicate(true)
	genes.predation = 1.0
	genes.scavenging = 1.0
	genes.coloniality = 1.0
	genes.differentiation = 1.0
	genes.sexual_reproduction = 1.0
	genes.luminescence = 0.5
	genes.armor = 1.0
	genes.resilience = 1.0
	var description := Summary.describe(genes)
	for term in ["Охота:","Органическое питание:","Цена пищевой специализации:","Специализация клеток:","Половое размножение:","Биолюминесценция:","токсинов"]:
		assert(term in description, "An expressed adaptation must explain its functional benefit or cost: " + term)
	assert("не гарантирует" in description and "не записаны" in Summary.describe({}))
	var child := sim.make_organism(founder.pos,genes,1,founder.id)
	sim.organisms.append(child)
	sim._register_species(child,founder.species,founder.genes)
	var tree_window := UI.show_species(root,sim)
	var view = tree_window.get_child(0)
	view.items[child.species].select(0)
	view.items[child.species].collapsed = true
	sim.organisms.erase(child)
	var grandchild_genes := genes.duplicate(true)
	grandchild_genes.speed = 90.0
	var grandchild := sim.make_organism(founder.pos,grandchild_genes,2,child.id)
	sim.organisms.append(grandchild)
	sim._register_species(grandchild,child.species,genes)
	tree_window.get_child(1).pressed.emit()
	assert(view.get_child_count() == 2, "Refreshing must not duplicate tree/detail widgets")
	assert(view.items.has(grandchild.species) and view.counts.get(child.species,0) == 0)
	assert(view.branches.get_selected().get_metadata(0) == child.species)
	assert(view.items[child.species].collapsed and "АДАПТАЦИИ" in view.details.text)
	sim._selected_id = grandchild.id
	var genome_window := UI.show_genome(root,grandchild,sim)
	var text: RichTextLabel = genome_window.get_child(0)
	grandchild.energy = 7.25
	assert("7.25" not in text.text, "Open inspection must remain a stable snapshot until refreshed")
	genome_window.get_child(1).pressed.emit()
	assert("7.25" in text.text and "АДАПТАЦИИ" in text.text)
	sim._selected_id = founder.id
	genome_window.get_child(1).pressed.emit()
	assert("#%d /" % int(founder.id) in text.text, "Refresh must inspect the currently selected organism")
	sim.organisms.clear()
	genome_window.get_child(1).pressed.emit()
	assert(text.text == RU.text("Select an organism in the observation chamber first."))
	sim.discovered_species.clear()
	tree_window.get_child(1).pressed.emit()
	assert(view.items.is_empty() and view.details.text == "Виды пока не зарегистрированы.")
	genome_window.queue_free()
	tree_window.queue_free()
	await process_frame
	print("Adaptation inspection tests passed: functional explanations, costs, legacy absence, refresh, branch state, new branches, current selection and empty experiment")
	quit(0)
