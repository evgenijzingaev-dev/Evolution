extends HSplitContainer

var records: Dictionary
var counts: Dictionary
var branches: Tree
var details: RichTextLabel
var items: Dictionary = {}
var root_item: TreeItem

func configure(sim: SimulationManager, selected_signature: String = "") -> void:
	var collapsed := {}
	if is_instance_valid(branches):
		var selected := branches.get_selected()
		if selected_signature.is_empty() and selected != null: selected_signature = str(selected.get_metadata(0))
		for signature in items: collapsed[signature] = items[signature].collapsed
	records = sim.discovered_species.duplicate(true)
	counts = sim.species_counts()
	if not is_instance_valid(branches):
		branches = Tree.new()
		branches.custom_minimum_size.x = 300
		branches.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		branches.hide_root = true
		add_child(branches)
		branches.item_selected.connect(_on_selected)
		details = RichTextLabel.new()
		details.custom_minimum_size.x = 400
		details.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		details.selection_enabled = true
		add_child(details)
	branches.clear()
	items.clear()
	root_item = branches.create_item()
	var signatures := records.keys()
	signatures.sort()
	for signature in signatures:
		_add_branch(str(signature), {})
	for signature in collapsed:
		if items.has(signature): items[signature].collapsed = bool(collapsed[signature])
	if items.has(selected_signature):
		items[selected_signature].select(0)
		branches.scroll_to_item(items[selected_signature])
	elif not signatures.is_empty():
		items[str(signatures[0])].select(0)
	else:
		details.text = "Виды пока не зарегистрированы."

func _add_branch(signature: String, visiting: Dictionary) -> TreeItem:
	if items.has(signature):
		return items[signature]
	var record: Dictionary = records[signature]
	var parent := root_item
	var ancestor := str(record.get("ancestor", "abiogenesis"))
	var path := visiting.duplicate()
	path[signature] = true
	if records.has(ancestor) and not path.has(ancestor):
		parent = _add_branch(ancestor, path)
	var item := branches.create_item(parent)
	item.set_text(0, "%s  ·  %d живых" % [signature, int(counts.get(signature, 0))])
	item.set_metadata(0, signature)
	items[signature] = item
	return item

func _on_selected() -> void:
	var item := branches.get_selected()
	if item == null:
		return
	var signature := str(item.get_metadata(0))
	var record: Dictionary = records[signature]
	var lines: Array[String] = ["SPECIES " + signature, "", "Parent: " + str(record.get("ancestor", "unknown")), "First detected: %.2f MYA / generation %d" % [float(record.get("first_seen", 0.0)) / 10.0, int(record.get("generation", 0))], "Living representatives: %d" % int(counts.get(signature, 0))]
	var extinct_at := float(record.get("extinct_at", -1.0))
	lines.append("Recorded extinction: %.2f MYA" % (extinct_at / 10.0) if extinct_at >= 0.0 else "No extinction recorded")
	lines.append("\nPHENOTYPE AT DISCOVERY: " + str(record.get("kind", "unknown")))
	lines.append("\nHEREDITARY CHANGES AT BRANCH ORIGIN")
	var changes: Array = record.get("changes", [])
	for change in changes:
		lines.append("%s: %.3f → %.3f" % [str(change.gene).capitalize(), float(change.from), float(change.to)])
	if changes.is_empty():
		lines.append("Founder lineage, or mutation details unavailable in this save.")
	lines.append("\nGENOME AT DISCOVERY")
	var genes: Dictionary = record.get("genes", {})
	if not genes.is_empty(): genes = GeneticsSystem.complete_genome(genes)
	if genes.has("coloniality") and genes.has("phototrophy") and genes.has("armor"):
		var body := preload("res://scripts/multicellular_system.gd").develop(genes)
		lines.append("Body: %d cells / feeding %d / motor %d / defense %d" % [body.cell_count, body.feeding_cells, body.motor_cells, body.defense_cells])
		lines.append("Feeding ×%.2f / movement ×%.2f / health ×%.2f / upkeep %.3f" % [body.feeding_multiplier, body.movement_multiplier, body.health_multiplier, body.upkeep])
	for gene_key in GeneticsSystem.TRAITS:
		if genes.has(gene_key):
			lines.append("%s: %.3f" % [str(gene_key).capitalize(), float(genes[gene_key])])
	if genes.is_empty():
		lines.append("Genome was not recorded in this older save.")
	else:
		lines.append("\n" + preload("res://scripts/adaptation_summary.gd").describe(genes))
	details.text = preload("res://scripts/localization_ru.gd").text("\n".join(lines))

