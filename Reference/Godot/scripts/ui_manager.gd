extends RefCounted

const SpeciesView = preload("res://scripts/species_view.gd")

static func show_history(parent: Node, sim: SimulationManager) -> Window:
	var window := _window(parent,"ИСТОРИЯ ЭВОЛЮЦИИ",Vector2i(900,620))
	window.min_size = Vector2i(640,500)
	var view := preload("res://scripts/evolution_history_view.gd").new()
	view.set_history(sim.timeline)
	window.add_child(view)
	view.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	view.offset_left = 16
	view.offset_top = 16
	view.offset_right = -16
	view.offset_bottom = -16
	view.refresh_requested.connect(func() -> void: view.set_history(sim.timeline))
	window.popup_centered()
	return window

static func show_archive(parent: Node, on_load: Callable) -> Window:
	var window := _window(parent, "АРХИВ ЭКСПЕРИМЕНТОВ", Vector2i(800, 540))
	var stack := VBoxContainer.new()
	window.add_child(stack)
	stack.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stack.offset_left = 16
	stack.offset_top = 16
	stack.offset_right = -16
	stack.offset_bottom = -16
	stack.add_theme_constant_override("separation", 12)
	var hint := Label.new()
	hint.text = "Каждое ручное сохранение оставляет отдельный снимок."
	stack.add_child(hint)
	var list := ItemList.new()
	list.name = "ArchiveEntries"
	list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stack.add_child(list)
	var entries := preload("res://scripts/experiment_archive.gd").entries()
	for entry in entries:
		var text := str(entry.label)
		if not str(entry.date).is_empty(): text += " · " + str(entry.date).replace("T", " ")
		if int(entry.population) >= 0: text += "\n%s · %d особей · %.2f млн лет" % [preload("res://scripts/localization_ru.gd").text(str(entry.planet)), int(entry.population), float(entry.elapsed) / 10.0]
		list.add_item(text)
	var load_button := Button.new()
	load_button.name = "LoadArchiveEntry"
	load_button.text = "ЗАГРУЗИТЬ ВЫБРАННЫЙ ЭКСПЕРИМЕНТ"
	load_button.disabled = entries.is_empty()
	stack.add_child(load_button)
	if entries.is_empty(): hint.text = "Сохранений пока нет."
	else: list.select(0)
	load_button.pressed.connect(func() -> void:
		var selected := list.get_selected_items()
		if not selected.is_empty():
			on_load.call(str(entries[selected[0]].path))
			window.queue_free()
	)
	window.popup_centered()
	return window

static func show_planet_settings(parent: Node, sim: SimulationManager, index: int, on_changed: Callable = Callable()) -> Window:
	var profile := sim.planet_data(index)
	var window := _window(parent, "УСЛОВИЯ ПЛАНЕТЫ · " + preload("res://scripts/localization_ru.gd").text(str(profile.name)), Vector2i(660, 680))
	var margin := MarginContainer.new()
	window.add_child(margin)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]: margin.add_theme_constant_override("margin_" + side, 16)
	var scroll := ScrollContainer.new()
	margin.add_child(scroll)
	var stack := VBoxContainer.new()
	stack.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	stack.add_theme_constant_override("separation", 10)
	scroll.add_child(stack)
	var hint := Label.new()
	hint.text = "Изменения влияют на условия и естественный отбор.\nГеномы и популяция сохраняются. Значения 0–1 — доли.\nШаблоны условий действуют и в новых опытах на этой планете."
	stack.add_child(hint)
	var labels := {"temperature":"Температура, °C","pressure":"Давление, бар","radiation":"Радиация","oxygen":"Кислород, мг/л","humidity":"Влажность","gravity":"Гравитация, g","water":"Доля воды","sunlight":"Солнечный свет","geology":"Геологическая активность","toxicity":"Токсичность","food_rate":"Продуктивность среды"}
	var fields := {}
	for key in PlanetManager.PARAMETER_RANGES:
		var row := HBoxContainer.new()
		stack.add_child(row)
		var label := Label.new()
		label.text = labels[key]
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(label)
		var field := SpinBox.new()
		field.name = key
		field.min_value = float(PlanetManager.PARAMETER_RANGES[key][0])
		field.max_value = float(PlanetManager.PARAMETER_RANGES[key][1])
		field.step = 0.01
		field.value = float(profile[key])
		field.custom_minimum_size.x = 150
		row.add_child(field)
		fields[key] = field
	var apply := Button.new()
	apply.name = "ApplyPlanetConditions"
	apply.text = "ПРИМЕНИТЬ УСЛОВИЯ"
	stack.add_child(apply)
	apply.pressed.connect(func() -> void:
		var values := {}
		for key in fields: values[key] = fields[key].value
		sim.configure_planet(index, values)
		if on_changed.is_valid(): on_changed.call()
		window.queue_free()
	)
	var reset := Button.new()
	reset.text = "ВОССТАНОВИТЬ ИСХОДНЫЕ УСЛОВИЯ"
	stack.add_child(reset)
	reset.pressed.connect(func() -> void:
		sim.reset_planet_conditions(index)
		if on_changed.is_valid(): on_changed.call()
		window.queue_free()
	)
	window.popup_centered()
	return window

static func show_events(parent: Node, sim: SimulationManager) -> Window:
	var window := _window(parent, "УПРАВЛЕНИЕ СРЕДОЙ", Vector2i(620, 400))
	var stack := VBoxContainer.new()
	window.add_child(stack)
	stack.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stack.offset_left = 16
	stack.offset_top = 16
	stack.offset_right = -16
	stack.offset_bottom = -16
	stack.add_theme_constant_override("separation", 16)
	var label := Label.new()
	label.text = "Выберите событие для текущей планеты."
	stack.add_child(label)
	var picker := OptionButton.new()
	picker.add_item("Случайное событие")
	for event in EnvironmentSystem.EVENTS:
		picker.add_item(preload("res://scripts/localization_ru.gd").text(str(event)))
	stack.add_child(picker)
	var hint := Label.new()
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hint.text = "Событие временно меняет климат, свет, влажность или атмосферу. Последствия для популяций и наследственных линий сохраняются после его окончания. Новое событие заменяет текущее."
	stack.add_child(hint)
	var apply := Button.new()
	apply.text = "ВЫЗВАТЬ СОБЫТИЕ"
	stack.add_child(apply)
	apply.pressed.connect(func() -> void:
		sim.trigger_environment_event(str(EnvironmentSystem.EVENTS[picker.selected - 1]) if picker.selected > 0 else "")
		window.queue_free()
	)
	window.popup_centered()
	return window

static func show_galaxy(parent: Node, selected: int, on_pick: Callable, overrides: Dictionary = {}) -> Window:
	var view := preload("res://scripts/galaxy_view.gd").new()
	view.selected_planet = selected
	view.planet_overrides = overrides.duplicate(true)
	return _selection_window(parent, "Галактика · выбор планеты", view, selected, on_pick)

static func show_landing(parent: Node, planet: int, selected: int, on_pick: Callable, profile: Dictionary = {}) -> Window:
	var view := preload("res://scripts/landing_map.gd").new()
	view.planet_index = planet
	view.selected_sector = selected
	view.planet_profile = profile.duplicate(true)
	return _selection_window(parent, "Карта биомов · место зарождения жизни", view, selected, on_pick)

static func _selection_window(parent: Node, title: String, view: Control, selected: int, on_pick: Callable) -> Window:
	var window := _window(parent, title, Vector2i(900, 560))
	window.min_size = Vector2i(780, 530)
	var margin := MarginContainer.new()
	window.add_child(margin)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 16)
	var stack := VBoxContainer.new()
	stack.add_theme_constant_override("separation", 12)
	margin.add_child(stack)
	var hint := Label.new()
	hint.text = "Выберите щелчком или стрелками, затем подтвердите выбор."
	stack.add_child(hint)
	view.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stack.add_child(view)
	var details := Label.new()
	details.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	details.custom_minimum_size.y = 76
	details.text = str(view.call("description", selected))
	stack.add_child(details)
	var confirm := Button.new()
	confirm.name = "ConfirmSelection"
	confirm.text = "ПОДТВЕРДИТЬ ВЫБОР"
	confirm.custom_minimum_size.y = 38
	confirm.disabled = selected < 0
	stack.add_child(confirm)
	var state := {"index":selected}
	view.connect("selection_changed", func(index: int) -> void:
		state.index = index
		details.text = str(view.call("description", index))
		confirm.disabled = false
	)
	confirm.pressed.connect(func() -> void:
		on_pick.call(int(state.index))
		window.queue_free()
	)
	window.popup_centered()
	return window

static func show_species(parent: Node, sim: SimulationManager) -> Window:
	var window := _window(parent, "SPECIES TREE / SELECT A BRANCH", Vector2i(940, 620))
	window.min_size = Vector2i(760,500)
	var view := SpeciesView.new()
	window.add_child(view)
	view.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	view.offset_left = 12
	view.offset_top = 12
	view.offset_right = -12
	view.offset_bottom = -12
	view.offset_top = 54
	var refresh := Button.new()
	refresh.text = "Обновить дерево"
	refresh.tooltip_text = "Обновить виды и численность текущего эксперимента; выбранная ветвь сохраняется."
	refresh.position = Vector2(12,12)
	refresh.pressed.connect(func() -> void: view.configure(sim))
	window.add_child(refresh)
	var selected := sim.selected()
	view.configure(sim, str(selected.get("species", "")))
	preload("res://scripts/localization_ru.gd").apply_tree(window)
	window.popup_centered()
	return window

static func show_genome(parent: Node, organism: Dictionary, sim: SimulationManager = null) -> Window:
	var window := _window(parent, "LIVE GENOME INSPECTOR", Vector2i(600, 650))
	var text := RichTextLabel.new()
	window.add_child(text)
	text.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	text.offset_left = 16
	text.offset_top = 16
	text.offset_right = -16
	text.offset_bottom = -16
	text.selection_enabled = true
	text.text = _genome_text(organism)
	if sim != null:
		text.offset_top = 56
		var refresh := Button.new()
		refresh.text = "Обновить выбранный организм"
		refresh.tooltip_text = "Показать новое состояние организма, выбранного сейчас на карте. До обновления показан снимок состояния."
		refresh.position = Vector2(16,12)
		refresh.pressed.connect(func() -> void:
			var scroll := text.get_v_scroll_bar().value
			text.text = _genome_text(sim.selected())
			text.get_v_scroll_bar().set_deferred("value",scroll)
		)
		window.add_child(refresh)
	preload("res://scripts/localization_ru.gd").apply_tree(window)
	window.popup_centered()
	return window

static func _genome_text(organism: Dictionary) -> String:
	if organism.is_empty():
		return preload("res://scripts/localization_ru.gd").text("Select an organism in the observation chamber first.")
	else:
		var lines: Array[String] = ["ORGANISM #%d / %s" % [organism.id, organism.kind], "Species: %s\nParent: #%d / generation %d" % [organism.species, organism.parent, organism.generation], "Age %.1f / energy %.2f / %s" % [organism.age, organism.energy, organism.behavior], ""]
		for gene_key in GeneticsSystem.TRAITS:
			lines.append("%s: %.3f" % [str(gene_key).capitalize(), float(organism.genes.get(gene_key, 0.0))])
		lines.append("\nHealth: %.2f / %.2f" % [float(organism.get("health", OrganismManager.max_health(organism.genes))), OrganismManager.max_health(organism.genes)])
		if int(organism.get("second_parent", -1)) >= 0:
			lines.append("Second parent: #%d" % int(organism.second_parent))
		var body: Dictionary = organism.get("body", {})
		lines.append("\nBODY: %d cells / feeding %d / motor %d / defense %d" % [int(body.get("cell_count", 1)), int(body.get("feeding_cells", 0)), int(body.get("motor_cells", 0)), int(body.get("defense_cells", 0))])
		lines.append("Body upkeep: %.3f energy/s" % float(body.get("upkeep", 0.0)))
		lines.append("\n" + preload("res://scripts/adaptation_summary.gd").describe(organism.genes))
		return preload("res://scripts/localization_ru.gd").text("\n".join(lines))

static func _window(parent: Node, title: String, dimensions: Vector2i) -> Window:
	var window := Window.new()
	window.title = title
	window.size = dimensions
	window.min_size = Vector2i(500, 400)
	parent.add_child(window)
	window.close_requested.connect(window.queue_free)
	return window

static func show_settings(parent: Node, sim: SimulationManager) -> Window:
	var window := _window(parent, "SIMULATION SETTINGS", Vector2i(560, 420))
	var stack := VBoxContainer.new()
	window.add_child(stack)
	stack.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stack.offset_left = 16
	stack.offset_top = 16
	stack.offset_right = -16
	stack.offset_bottom = -16
	stack.add_theme_constant_override("separation", 14)
	var fields := {}
	var definitions := [["mutation_rate", "Mutation chance per gene", 0.0, 1.0, 0.01], ["mutation_strength", "Mutation strength", 0.0, 0.5, 0.01], ["resource_regrowth", "Nutrient recovery multiplier", 0.0, 5.0, 0.1], ["population_limit", "Population ceiling (births)", 1.0, 10000.0, 1.0]]
	for definition in definitions:
		var row := HBoxContainer.new()
		stack.add_child(row)
		var label := Label.new()
		label.text = str(definition[1])
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(label)
		var field := SpinBox.new()
		field.min_value = float(definition[2])
		field.max_value = float(definition[3])
		field.step = float(definition[4])
		field.value = float(sim.settings[definition[0]])
		field.custom_minimum_size.x = 150
		row.add_child(field)
		fields[definition[0]] = field
	var hint := Label.new()
	hint.text = "Changes affect future births and resource recovery.\nLowering the ceiling preserves existing organisms."
	stack.add_child(hint)
	var apply := Button.new()
	apply.text = "APPLY SETTINGS"
	stack.add_child(apply)
	apply.pressed.connect(func() -> void:
		var values := {}
		for key in fields:
			values[key] = fields[key].value
		sim.configure_settings(values)
		window.queue_free()
	)
	preload("res://scripts/localization_ru.gd").apply_tree(window)
	window.popup_centered()
	return window

