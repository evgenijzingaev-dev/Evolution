extends SceneTree

const RU = preload("res://scripts/localization_ru.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	for gene in GeneticsSystem.TRAITS:
		assert(RU.text(str(gene).capitalize()) != str(gene).capitalize(), "Every exposed gene requires a Russian label: " + str(gene))
	for event in EnvironmentSystem.EVENTS:
		assert(RU.text(str(event)) != str(event), "Every event requires Russian text")
	for planet in PlanetManager.PLANETS:
		assert(RU.text(str(planet.name)) != str(planet.name))
	for index in range(5):
		for biome in BiomeManager.biomes_for_planet(index):
			assert(RU.text(str(biome.name)) != str(biome.name))
	var scene: Node = load("res://scenes/main.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	var sim: SimulationManager = scene.get("sim")
	sim._selected_id = int(sim.organisms[0].id)
	scene.call("_refresh_ui")
	scene.call("_show_genome")
	scene.call("_show_lineage")
	scene.call("_show_history")
	preload("res://scripts/ui_manager.gd").show_settings(scene, sim)
	RU.apply_tree(scene)
	var missing: Array[String] = []
	_collect(scene, missing)
	for value in missing:
		push_error("Untranslated visible text: " + value)
	assert(missing.is_empty(), "Visible menu and inspection text must be Russian")
	scene.queue_free()
	print("Russian localization tests passed: genes, events, planets, biomes, setup, HUD, inspectors and settings")
	quit(0)

func _collect(node: Node, missing: Array[String]) -> void:
	var values: Array[String] = []
	if node is Label or node is Button or node is RichTextLabel:
		values.append(node.text)
	if node is OptionButton:
		for i in range(node.item_count): values.append(node.get_item_text(i))
	if node is LineEdit: values.append(node.placeholder_text)
	if node is Window: values.append(node.title)
	var latin := RegEx.new()
	latin.compile("[A-Za-z]{2,}")
	for value in values:
		if latin.search(value) != null: missing.append(value)
	for child in node.get_children(): _collect(child, missing)
