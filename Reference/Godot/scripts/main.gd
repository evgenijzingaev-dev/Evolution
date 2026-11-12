extends Node2D

const RU = preload("res://scripts/localization_ru.gd")

const Simulation = preload("res://scripts/simulation_manager.gd")
const Save = preload("res://scripts/save_manager.gd")
const Statistics = preload("res://scripts/statistics_view.gd")
const World = preload("res://scripts/world_view.gd")
const BG := Color("071522")
const PANEL := Color("0c1d2b")
const PANEL_ALT := Color("102535")
const TEXT := Color("d6e7ef")
const MUTED := Color("7f9baa")
const CYAN := Color("54d7d2")
const GOLD := Color("ffd18a")

var sim: Simulation
var world_view: WorldView
var ui: Control
var organisms_label: Label
var generation_label: Label
var mutation_label: Label
var lifespan_label: Label
var speed_label: Label
var clock_label: Label
var status_label: Label
var selected_label: Label
var planet_name_label: Label
var biome_label: Label
var discovered_label: Label
var event_label: Label
var temperature_label: Label
var pressure_label: Label
var radiation_label: Label
var oxygen_label: Label
var humidity_label: Label
var gravity_label: Label
var sunlight_label: Label
var stability_label: Label
var stability_meter: Control
var atmosphere_label: Label
var _pending_save := false
var _pending_load_path := ""
var _file_message := ""
var _file_message_until := 0
var autosave_timer := 0.0
var pause_button: Button
var speed_button: Button
var last_ui_refresh := 0.0
var experiment_setup: Control
var planet_picker: OptionButton
var founder_slider: HSlider
var founder_count_label: Label
var seed_input: LineEdit
var experiment_summary: Label
var experiment_scrim: ColorRect
var experiment_center: CenterContainer
var experiment_started := false
var pause_before_setup := false
var resume_experiment_button: Button
var setup_message: Label
var sector_picker: OptionButton

func _ready() -> void:
	get_window().title = "ЭВОЛЮЦИЯ: ЖИЗНЬ ЗА ПРЕДЕЛАМИ"
	sim = Simulation.new()
	sim.paused = true
	world_view = World.new()
	world_view.position = Vector2(310, 158)
	world_view.size = Vector2(820, 618)
	world_view.sim = sim
	add_child(world_view)
	_build_ui()
	_build_experiment_setup()
	_resize_ui_layers()
	_refresh_ui()

func _process(delta: float) -> void:
	if sim == null:
		return
	_resize_ui_layers()
	_update_bounds()
	world_view.position = sim.sim_bounds.position
	world_view.size = sim.sim_bounds.size
	if not _process_file_requests():
		sim.tick(delta)
	if experiment_started and not experiment_setup.visible:
		autosave_timer += delta
	if experiment_started and autosave_timer >= 120.0 and not sim._step_active and not _pending_save and _pending_load_path.is_empty():
		var autosave_error := Save.save_simulation(sim, Save.AUTOSAVE_PATH)
		autosave_timer = 0.0 if autosave_error == OK else 115.0
		if autosave_error != OK: _file_status("Не удалось выполнить автосохранение. Повтор через 5 секунд.")
	if Time.get_ticks_msec() / 1000.0 - last_ui_refresh > 0.35:
		_refresh_ui()
		last_ui_refresh = Time.get_ticks_msec() / 1000.0
	queue_redraw()

func _draw() -> void:
	var size := get_viewport_rect().size
	draw_rect(Rect2(Vector2.ZERO, size), BG)
	# Atmospheric orbital halos
	draw_circle(Vector2(720, 460), 500, Color(0.08, 0.37, 0.46, 0.055))
	draw_circle(Vector2(720, 460), 370, Color(0.08, 0.37, 0.46, 0.045))
	_draw_top_bar(size)
	var planet := sim.planet_data()
	var chamber_biome := BiomeManager.at_position(sim.sim_bounds.get_center(), sim.sim_bounds, sim.planet_index)
	_draw_panel(Rect2(24, 96, 258, size.y - 128), "PLANETARY DATA", "%02d / %s" % [sim.planet_index + 1, planet.name.to_upper()])
	_draw_panel(Rect2(298, 96, size.x - 584, size.y - 128), "OBSERVATION CHAMBER", "%s · ENVIRONMENTAL STUDY" % chamber_biome.name.to_upper())
	_draw_panel(Rect2(size.x - 272, 96, 248, size.y - 128), "SPECIMEN ANALYSIS", "LIVE GENOME")
	if sim.organisms.is_empty():
		draw_string(ThemeDB.fallback_font, Vector2(540, 450), RU.text("NO LIFE DETECTED"), HORIZONTAL_ALIGNMENT_LEFT, -1, 22, TEXT)

func _draw_top_bar(size: Vector2) -> void:
	draw_rect(Rect2(0, 0, size.x, 76), Color("091824"))
	draw_line(Vector2(0, 76), Vector2(size.x, 76), Color(CYAN, 0.28), 1)
	draw_circle(Vector2(43, 37), 16, Color(CYAN, 0.14))
	draw_arc(Vector2(43, 37), 12, -0.8, 4.0, 30, CYAN, 1.4)
	draw_string(ThemeDB.fallback_font, Vector2(70, 35), RU.text("EVOLUTION"), HORIZONTAL_ALIGNMENT_LEFT, -1, 20, TEXT)
	draw_string(ThemeDB.fallback_font, Vector2(71, 55), RU.text("LIFE BEYOND   /   BIOSPHERE LAB"), HORIZONTAL_ALIGNMENT_LEFT, -1, 9, MUTED)
	draw_string(ThemeDB.fallback_font, Vector2(size.x - 250, 34), RU.text("SIMULATION PAUSED" if sim.paused else "SIMULATION  ONLINE"), HORIZONTAL_ALIGNMENT_RIGHT, 222, 10, CYAN)
	draw_circle(Vector2(size.x - 265, 30), 3, CYAN)

func _draw_panel(rect: Rect2, title: String, subtitle: String) -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = PANEL
	style.border_color = Color(CYAN, 0.12)
	style.set_border_width_all(1)
	style.set_corner_radius_all(8)
	draw_style_box(style, rect)
	draw_string(ThemeDB.fallback_font, rect.position + Vector2(16, 26), RU.text(title), HORIZONTAL_ALIGNMENT_LEFT, -1, 9, MUTED)
	draw_string(ThemeDB.fallback_font, rect.position + Vector2(16, 49), RU.text(subtitle), HORIZONTAL_ALIGNMENT_LEFT, -1, 14, CYAN)
	draw_line(rect.position + Vector2(16, 61), Vector2(rect.end.x - 16, rect.position.y + 61), Color(CYAN, 0.15), 1)

func _draw_water() -> void:
	var r := sim.sim_bounds
	draw_rect(r, Color("09243a"))
	var biomes: Array = BiomeManager.biomes_for_planet(sim.planet_index)
	for x in range(4):
		for y in range(3):
			var biome: Dictionary = biomes[posmod(x + y * 2, biomes.size())]
			var rect := Rect2(r.position.x + x * r.size.x / 4.0, r.position.y + y * r.size.y / 3.0, r.size.x / 4.0 + 1.0, r.size.y / 3.0 + 1.0)
			draw_rect(rect, Color(str(biome.color)))
			draw_rect(rect, Color(CYAN, 0.045), false, 1)
	for i in range(15):
		var x := r.position.x + fmod(float(i) * 67.0 + fposmod(Time.get_ticks_msec() * 0.012, 67.0), r.size.x)
		var y := r.position.y + fmod(float(i * 91) + sin(Time.get_ticks_msec() * 0.0005 + i) * 24.0, r.size.y)
		draw_circle(Vector2(x, y), 1.5 + fposmod(i, 3.0), Color(0.48, 0.85, 0.9, 0.18))
	draw_rect(r, Color(CYAN, 0.14), false, 1)

func _draw_nutrient() -> void:
	for point in sim.food:
		draw_circle(point, 5.5, Color(GOLD, 0.10))
		draw_circle(point, 2.1, GOLD)

func _draw_organisms() -> void:
	var stride := maxi(1, ceili(float(sim.organisms.size()) / 2500.0))
	for organism_index in range(sim.organisms.size()):
		if stride > 1 and organism_index % stride != 0 and int(sim.organisms[organism_index].id) != sim._selected_id:
			continue
		var o: Dictionary = sim.organisms[organism_index]
		var genes: Dictionary = o.genes
		var radius := 2.0 + float(genes.size) * 2.1 + float(genes.coloniality) * 2.0
		var signature: String = o.species
		var hue := float(abs(signature.hash()) % 360) / 360.0
		var body_color := Color.from_hsv(hue, 0.58, 0.95)
		match str(o.kind):
			"Producer": body_color = Color("76e6a2")
			"Predator": body_color = Color("ff6f88")
			"Colony": body_color = Color("c7a1ff")
		if int(o.id) == sim._selected_id:
			draw_arc(o.pos, radius + 5.0, 0, TAU, 24, Color.WHITE, 1.2)
		draw_circle(o.pos, radius + 2.5 + float(genes.luminescence) * 6.0, Color(body_color, 0.10 + float(genes.luminescence) * 0.25))
		draw_circle(o.pos, radius, body_color)
		if float(genes.armor) > 0.2:
			draw_arc(o.pos, radius + 1.0, 0, TAU, 12, Color(0.88, 0.92, 1.0, minf(0.95, float(genes.armor))), 1.0 + float(genes.armor))
		draw_circle(o.pos + Vector2(cos(o.heading), sin(o.heading)) * radius * 0.48, maxf(1.0, radius * 0.23), Color("edffff"))
		var appendages := 1 + roundi(float(genes.cilia) * 5.0 + float(genes.limbs) * 3.0)
		for appendage in range(appendages):
			var angle: float = o.heading + float(appendage) * TAU / float(appendages) + 1.2
			var fin: Vector2 = o.pos + Vector2(cos(angle), sin(angle)) * radius * (1.35 + float(genes.limbs) * 0.6)
			draw_line(o.pos, fin, Color(body_color, 0.35 + float(genes.cilia) * 0.55), 0.8 + float(genes.limbs))

func _draw_overlay_labels(size: Vector2) -> void:
	draw_string(ThemeDB.fallback_font, Vector2(326, 158), RU.text("NUTRIENT CURRENT"), HORIZONTAL_ALIGNMENT_LEFT, -1, 9, Color(CYAN, 0.72))
	draw_string(ThemeDB.fallback_font, Vector2(1100, size.y - 58), RU.text("GRID 12 × 8   ·   SPATIAL SEARCH ACTIVE"), HORIZONTAL_ALIGNMENT_RIGHT, -1, 8, MUTED)

func _build_ui() -> void:
	ui = Control.new()
	ui.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ui.set_anchors_preset(Control.PRESET_TOP_LEFT)
	ui.mouse_filter = Control.MOUSE_FILTER_PASS
	add_child(ui)
	var settings_button := _add_button(ui, "SETTINGS", 360, 20, 110)
	settings_button.pressed.connect(func() -> void: preload("res://scripts/ui_manager.gd").show_settings(self, sim))
	var galaxy_button := _add_button(ui, "ГАЛАКТИКА", 480, 20, 110)
	galaxy_button.pressed.connect(_show_galaxy)
	var conditions_button := _add_button(ui, "УСЛОВИЯ ПЛАНЕТЫ", 600, 20, 150)
	conditions_button.pressed.connect(func() -> void: _show_planet_conditions(sim.planet_index))
	_add_label(ui, "BIOLOGICAL CONDITIONS", 42, 176, 220, 18, 9, MUTED)
	atmosphere_label = _add_label(ui, "", 42, 200, 218, 18, 8, MUTED)
	_add_label(ui, "Mean temperature", 42, 220, 140, 16, 10, MUTED)
	temperature_label = _add_label(ui, "18.4 °C", 42, 242, 210, 28, 23, TEXT)
	_add_label(ui, "Pressure", 42, 293, 140, 16, 10, MUTED)
	pressure_label = _add_label(ui, "1.02 bar", 42, 314, 210, 24, 18, TEXT)
	_add_label(ui, "Radiation", 42, 361, 140, 16, 10, MUTED)
	radiation_label = _add_label(ui, "LOW   ·   0.14 mSv", 42, 384, 220, 20, 12, CYAN)
	_add_label(ui, "Dissolved oxygen", 42, 428, 150, 16, 10, MUTED)
	oxygen_label = _add_label(ui, "7.8 mg/L", 42, 450, 210, 24, 18, TEXT)
	_add_label(ui, "BIOME CONDITIONS", 42, 501, 180, 16, 8, MUTED)
	humidity_label = _add_label(ui, "Humidity  ·  78%", 42, 527, 210, 15, 10, TEXT)
	gravity_label = _add_label(ui, "Gravity  ·  1.00 g", 42, 552, 210, 15, 10, TEXT)
	sunlight_label = _add_label(ui, "Solar flux  ·  82%", 42, 577, 210, 15, 10, TEXT)
	_add_label(ui, "ECOSYSTEM STABILITY", 42, 612, 180, 16, 8, MUTED)
	stability_label = _add_label(ui, "DYNAMIC  ·  ADAPTING", 42, 635, 210, 16, 10, GOLD)
	stability_meter = _make_progress(42, 660, 218, 5, 0.62, CYAN)
	ui.add_child(stability_meter)
	planet_name_label = _add_label(ui, "TERRA", 42, 677, 196, 18, 10, CYAN)
	biome_label = _add_label(ui, "PELAGIC ZONE", 42, 697, 208, 16, 8, MUTED)
	var planet_button := _add_button(ui, "NEXT PLANET  →", 42, 722, 218)
	planet_button.pressed.connect(_on_next_planet)
	var event_button := _add_button(ui, "TRIGGER EVENT", 42, 765, 218)
	event_button.pressed.connect(_on_trigger_event)
	_add_label(ui, "CURRENT POPULATION", 1186, 176, 210, 16, 8, MUTED)
	_add_label(ui, "34", 1186, 204, 216, 38, 32, TEXT, "organisms_label")
	_add_label(ui, "individual organisms", 1186, 240, 210, 16, 9, MUTED)
	_add_label(ui, "Discovered species", 1186, 281, 210, 16, 9, MUTED)
	_add_label(ui, "—", 1186, 302, 210, 24, 18, CYAN, "discovered_label")
	_add_label(ui, "Total generations", 1186, 345, 210, 16, 9, MUTED)
	_add_label(ui, "0", 1186, 366, 210, 24, 18, TEXT, "generation_label")
	_add_label(ui, "Recorded mutations", 1186, 408, 210, 16, 9, MUTED)
	_add_label(ui, "0", 1186, 429, 210, 24, 18, TEXT, "mutation_label")
	_add_label(ui, "Mean life span", 1186, 471, 210, 16, 9, MUTED)
	_add_label(ui, "—", 1186, 492, 210, 24, 18, TEXT, "lifespan_label")
	_add_label(ui, "Mean swim speed", 1186, 534, 210, 16, 9, MUTED)
	_add_label(ui, "—", 1186, 555, 210, 24, 18, TEXT, "speed_label")
	_add_label(ui, "SELECTION", 1186, 603, 210, 14, 8, MUTED)
	_add_label(ui, "Select an organism", 1186, 626, 210, 60, 9, MUTED, "selected_label")
	event_label = _add_label(ui, "NO ACTIVE EVENT", 1186, 700, 212, 42, 8, GOLD)
	var footer := Panel.new()
	footer.position = Vector2(300, 796)
	footer.size = Vector2(840, 58)
	var footer_style := StyleBoxFlat.new()
	footer_style.bg_color = Color("0a1926")
	footer_style.border_color = Color(CYAN, 0.22)
	footer_style.set_border_width_all(1)
	footer_style.set_corner_radius_all(8)
	footer.add_theme_stylebox_override("panel", footer_style)
	ui.add_child(footer)
	clock_label = _add_label(footer, "YEAR 0000.0 MYA", 20, 22, 150, 20, 10, CYAN)
	pause_button = _add_button(footer, "Ⅱ  PAUSE", 194, 10, 104)
	pause_button.pressed.connect(_on_pause)
	var restart_button := _add_button(footer, "NEW EXP", 306, 10, 100)
	restart_button.pressed.connect(_show_experiment_setup)
	speed_button = _add_button(footer, "×1  SPEED", 414, 10, 104)
	speed_button.pressed.connect(_on_speed)
	var save_button := _add_button(footer, "SAVE", 530, 10, 65)
	save_button.pressed.connect(_on_save)
	var load_button := _add_button(footer, "LOAD", 600, 10, 65)
	load_button.pressed.connect(_on_load)
	status_label = _add_label(footer, "ECOSYSTEM   ·   RUNNING", 676, 22, 150, 18, 8, MUTED)
	status_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_add_label(ui, "SPACE  PAUSE   /   +  SPEED UP   /   CLICK  SPECIMEN", 422, 865, 600, 14, 8, MUTED)
	var statistics_button := _add_button(ui, "STATS", 1184, 755, 66)
	statistics_button.pressed.connect(_show_statistics)
	var history_button := _add_button(ui, "EVENTS", 1254, 755, 66)
	history_button.pressed.connect(_show_history)
	var lineage_button := _add_button(ui, "TREE", 1324, 755, 66)
	lineage_button.pressed.connect(_show_lineage)
	var genome_button := _add_button(ui, "FULL GENOME", 1184, 800, 206)
	genome_button.pressed.connect(_show_genome)

func _add_label(parent: Control, value: String, x: float, y: float, width: float, height: float, font_size: int, color: Color, field: String = "") -> Label:
	var label := Label.new()
	label.text = value
	label.position = Vector2(x, y)
	label.size = Vector2(width, height)
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	parent.add_child(label)
	if field != "":
		set(field, label)
	return label

func _add_button(parent: Control, value: String, x: float, y: float, width: float) -> Button:
	var b := Button.new()
	b.text = value
	b.position = Vector2(x, y)
	b.size = Vector2(width, 36)
	b.add_theme_font_size_override("font_size", 9)
	var normal := StyleBoxFlat.new()
	normal.bg_color = PANEL_ALT
	normal.border_color = Color(CYAN, 0.24)
	normal.set_border_width_all(1)
	normal.set_corner_radius_all(5)
	b.add_theme_stylebox_override("normal", normal)
	var hover := normal.duplicate()
	hover.bg_color = Color("17384a")
	b.add_theme_stylebox_override("hover", hover)
	b.add_theme_color_override("font_color", TEXT)
	parent.add_child(b)
	return b

func _make_progress(x: float, y: float, width: float, height: float, amount: float, tint: Color) -> Control:
	var meter := Control.new()
	meter.position = Vector2(x, y)
	meter.size = Vector2(width, height)
	meter.draw.connect(func() -> void:
		meter.draw_rect(Rect2(Vector2.ZERO, meter.size), Color("1c3543"))
		meter.draw_rect(Rect2(Vector2.ZERO, Vector2(meter.size.x * sim.ecosystem_stability, meter.size.y)), tint)
	)
	return meter

func _update_bounds() -> void:
	var size := get_viewport_rect().size
	sim.set_bounds(Rect2(310, 158, maxf(320, size.x - 620), maxf(300, size.y - 282)))

func _refresh_ui() -> void:
	if organisms_label == null:
		return
	organisms_label.text = _format_int(sim.organisms.size())
	discovered_label.text = "%02d" % sim.discovered_species.size()
	var generations := 0
	for o in sim.organisms:
		generations = maxi(generations, int(o.generation))
	generation_label.text = "%d" % generations
	mutation_label.text = _format_int(sim.mutations)
	lifespan_label.text = "%.0f s" % (sim.lifespan_sum / sim.lifespan_count) if sim.lifespan_count > 0 else "—"
	var speed_sum := 0.0
	for o in sim.organisms:
		speed_sum += float(o.genes.speed)
	speed_label.text = "%.1f u/s" % (speed_sum / sim.organisms.size()) if not sim.organisms.is_empty() else "—"
	clock_label.text = "YEAR %07.2f MYA" % (sim.elapsed / 10.0)
	pause_button.text = "▶  RESUME" if sim.paused else "Ⅱ  PAUSE"
	speed_button.text = "МАКС" if sim.speed_index == Simulation.MAX_SPEED_INDEX else "×%s  SPEED" % str(Simulation.SPEEDS[sim.speed_index])
	speed_button.tooltip_text = "Переключить скорость: ×1 → ×2 → ×5 → ×10 → МАКС. Максимальное ускорение зависит от размера популяции."
	status_label.text = "POPULATION COLLAPSED" if sim.organisms.is_empty() else ("SIMULATION PAUSED" if sim.paused else "AQUATIC ECOSYSTEM   ·   RUNNING")
	if Time.get_ticks_msec() < _file_message_until: status_label.text = _file_message
	var planet := sim.planet_data()
	temperature_label.text = "%.1f °C" % (float(planet.temperature) + sim.environment.temperature_shift)
	pressure_label.text = "%.2f bar" % planet.pressure
	radiation_label.text = "%s   ·   %.2f mSv" % ["LOW" if float(planet.radiation) < 0.5 else "ELEVATED", float(planet.radiation) * sim.environment.radiation_multiplier]
	oxygen_label.text = "%.1f mg/L" % (float(planet.oxygen) * sim.environment.oxygen_multiplier)
	atmosphere_label.text = "Атмосфера: " + str(planet.atmosphere)
	atmosphere_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	atmosphere_label.tooltip_text = atmosphere_label.text
	humidity_label.text = "Humidity  ·  %d%%" % roundi(clampf(float(planet.humidity) * sim.environment.humidity_multiplier, 0.0, 1.0) * 100.0)
	gravity_label.text = "Gravity  ·  %.2f g" % planet.gravity
	sunlight_label.text = "Solar flux  ·  %d%%" % roundi(float(planet.sunlight) * sim.environment.sunlight_multiplier * 100.0)
	stability_label.text = "%.0f%%  ·  %s" % [sim.ecosystem_stability * 100.0, "STABLE" if sim.ecosystem_stability > 0.7 else "ADAPTING"]
	stability_meter.queue_redraw()
	planet_name_label.text = planet.name.to_upper()
	var viewed_biome := BiomeManager.at_position(sim.sim_bounds.get_center(), sim.sim_bounds, sim.planet_index)
	biome_label.text = str(viewed_biome.name).to_upper()
	event_label.text = "NO ACTIVE EVENT" if sim.environment.current_event.is_empty() else "%s\n%.0f SIM YEARS LEFT" % [sim.environment.current_event.to_upper(), sim.environment.event_remaining]
	var selected := sim.selected()
	if selected.is_empty():
		selected_label.text = "Select an organism"
	else:
		var g: Dictionary = selected.genes
		var selected_biome := BiomeManager.at_position(selected.pos, sim.sim_bounds, sim.planet_index)
		biome_label.text = str(selected_biome.name).to_upper()
		var offspring := 0
		for organism in sim.organisms:
			if int(organism.parent) == int(selected.id) or int(organism.get("second_parent", -1)) == int(selected.id):
				offspring += 1
		selected_label.text = "%s · %s\nGen %d | parent #%d\nOffspring %d | energy %.0f\nSpeed %.1f | armor %.2f\nPhoto %.2f | hunt %.2f" % [selected.kind, selected.behavior, selected.generation, selected.parent, offspring, selected.energy, g.speed, g.armor, g.phototrophy, g.predation]

	RU.apply_tree(self)

func _format_int(value: int) -> String:
	var raw := str(value)
	var result := ""
	for i in range(raw.length()):
		if i > 0 and (raw.length() - i) % 3 == 0:
			result += ","
		result += raw[i]
	return result

func _on_pause() -> void:
	sim.paused = not sim.paused
	_refresh_ui()

func _on_speed() -> void:
	sim.speed_index = (sim.speed_index + 1) % Simulation.SPEEDS.size()
	if sim.speed_index == 0:
		sim.speed_index = 1
	_refresh_ui()

func _on_reset() -> void:
	sim.seed_world()
	_refresh_ui()

func _build_experiment_setup() -> void:
	experiment_setup = Control.new()
	experiment_setup.name = "ExperimentSetup"
	experiment_setup.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	experiment_setup.mouse_filter = Control.MOUSE_FILTER_STOP
	ui.add_child(experiment_setup)
	experiment_scrim = ColorRect.new()
	experiment_scrim.color = Color(0.015, 0.045, 0.075, 0.94)
	experiment_setup.add_child(experiment_scrim)
	experiment_center = CenterContainer.new()
	experiment_setup.add_child(experiment_center)
	var card := PanelContainer.new()
	card.custom_minimum_size = Vector2(620, 610)
	var card_style := StyleBoxFlat.new()
	card_style.bg_color = Color("0c1d2b")
	card_style.border_color = Color(CYAN, 0.45)
	card_style.set_border_width_all(1)
	card_style.set_corner_radius_all(12)
	card_style.content_margin_left = 30
	card_style.content_margin_right = 30
	card_style.content_margin_top = 28
	card_style.content_margin_bottom = 24
	card.add_theme_stylebox_override("panel", card_style)
	experiment_center.add_child(card)
	var stack := VBoxContainer.new()
	stack.add_theme_constant_override("separation", 12)
	card.add_child(stack)
	var title := Label.new()
	title.text = "EVOLUTION  /  LIFE BEYOND"
	title.add_theme_font_size_override("font_size", 23)
	title.add_theme_color_override("font_color", TEXT)
	stack.add_child(title)
	var subtitle := Label.new()
	subtitle.text = "EXPERIMENT SETUP     ·     SELECT A WORLD AND SEED LIFE"
	subtitle.add_theme_font_size_override("font_size", 10)
	subtitle.add_theme_color_override("font_color", CYAN)
	stack.add_child(subtitle)
	_add_setup_heading(stack, "01   PLANETARY SYSTEM")
	planet_picker = OptionButton.new()
	planet_picker.custom_minimum_size.y = 38
	for planet in PlanetManager.PLANETS:
		planet_picker.add_item("%s   ·   %s" % [str(planet.name), str(planet.biome)])
	planet_picker.item_selected.connect(_update_experiment_summary)
	stack.add_child(planet_picker)
	_add_setup_heading(stack, "LANDING SITE / LOCAL BIOME")
	sector_picker = OptionButton.new()
	sector_picker.custom_minimum_size.y = 34
	stack.add_child(sector_picker)
	var map_actions := HBoxContainer.new()
	stack.add_child(map_actions)
	var galaxy := Button.new()
	galaxy.text = "ВЫБРАТЬ В ГАЛАКТИКЕ"
	galaxy.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	galaxy.pressed.connect(_show_galaxy)
	map_actions.add_child(galaxy)
	var landing := Button.new()
	landing.text = "ОТКРЫТЬ КАРТУ БИОМОВ"
	landing.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	landing.pressed.connect(_show_landing_map)
	map_actions.add_child(landing)
	var climate := Button.new()
	climate.text = "КЛИМАТ"
	climate.pressed.connect(func() -> void: _show_planet_conditions(planet_picker.selected))
	map_actions.add_child(climate)
	_add_setup_heading(stack, "02   FOUNDING POPULATION")
	founder_slider = HSlider.new()
	founder_slider.min_value = 1
	founder_slider.max_value = 100
	founder_slider.step = 1
	founder_slider.value = 1
	founder_slider.value_changed.connect(_on_founder_count_changed)
	stack.add_child(founder_slider)
	founder_count_label = Label.new()
	founder_count_label.text = "1 primitive cell"
	founder_count_label.add_theme_color_override("font_color", TEXT)
	stack.add_child(founder_count_label)
	_add_setup_heading(stack, "03   RANDOM SEED   ·   OPTIONAL")
	seed_input = LineEdit.new()
	seed_input.placeholder_text = "Blank for a new random experiment"
	seed_input.custom_minimum_size.y = 36
	seed_input.max_length = 10
	stack.add_child(seed_input)
	experiment_summary = Label.new()
	experiment_summary.custom_minimum_size = Vector2(0, 70)
	experiment_summary.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	experiment_summary.add_theme_color_override("font_color", MUTED)
	stack.add_child(experiment_summary)
	var launch := Button.new()
	launch.text = "▶   BEGIN EVOLUTION"
	launch.custom_minimum_size.y = 46
	launch.add_theme_font_size_override("font_size", 14)
	launch.add_theme_color_override("font_color", TEXT)
	launch.pressed.connect(_begin_experiment)
	stack.add_child(launch)
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 12)
	stack.add_child(actions)
	var load_experiment := Button.new()
	load_experiment.text = "АРХИВ ЭКСПЕРИМЕНТОВ"
	load_experiment.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	load_experiment.pressed.connect(_on_load)
	actions.add_child(load_experiment)
	var load_auto := Button.new()
	load_auto.text = "LOAD AUTOSAVE"
	load_auto.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	load_auto.pressed.connect(_load_from_setup.bind(Save.AUTOSAVE_PATH))
	actions.add_child(load_auto)
	resume_experiment_button = Button.new()
	resume_experiment_button.text = "RETURN TO EXPERIMENT"
	resume_experiment_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	resume_experiment_button.pressed.connect(_return_to_experiment)
	resume_experiment_button.hide()
	actions.add_child(resume_experiment_button)
	setup_message = Label.new()
	setup_message.add_theme_color_override("font_color", GOLD)
	setup_message.add_theme_font_size_override("font_size", 11)
	stack.add_child(setup_message)
	var hint := Label.new()
	hint.text = "Planet conditions shape survival, available energy and local biomes.\nAn empty seed field creates a fresh randomized world."
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint.add_theme_font_size_override("font_size", 9)
	hint.add_theme_color_override("font_color", MUTED)
	stack.add_child(hint)
	planet_picker.select(sim.planet_index)
	_update_experiment_summary(sim.planet_index)

func _resize_ui_layers() -> void:
	var viewport_size := get_viewport_rect().size
	if ui != null:
		ui.position = Vector2.ZERO
		ui.size = viewport_size
	if experiment_setup != null:
		experiment_setup.position = Vector2.ZERO
		experiment_setup.size = viewport_size
		experiment_scrim.position = Vector2.ZERO
		experiment_scrim.size = viewport_size
		experiment_center.position = Vector2.ZERO
		experiment_center.size = viewport_size

func _add_setup_heading(parent: Control, text: String) -> void:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 9)
	label.add_theme_color_override("font_color", CYAN)
	parent.add_child(label)

func _on_founder_count_changed(value: float) -> void:
	var count := roundi(value)
	founder_count_label.text = "%d %s" % [count, "primitive cell" if count == 1 else "founder cells"]

func _update_experiment_summary(index: int) -> void:
	if experiment_summary == null:
		return
	var planet := sim.planet_data(index)
	if sector_picker != null:
		sector_picker.clear()
		sector_picker.add_item("Random location", -1)
		for sector in range(12):
			var point := sim.sim_bounds.position + Vector2((float(sector % 4) + 0.5) * sim.sim_bounds.size.x / 4.0, (float(sector / 4) + 0.5) * sim.sim_bounds.size.y / 3.0)
			var biome := BiomeManager.at_position(point, sim.sim_bounds, index)
			sector_picker.add_item("Sector %02d / %s" % [sector + 1, biome.name], sector)
	experiment_summary.text = "T %.1f °C     PRESSURE %.2f bar     RADIATION %.2f\nO₂ %.1f mg/L     WATER %d%%     SOLAR %d%%     GEOLOGY %d%%" % [planet.temperature, planet.pressure, planet.radiation, planet.oxygen, roundi(float(planet.water) * 100.0), roundi(float(planet.sunlight) * 100.0), roundi(float(planet.geology) * 100.0)]

func _show_experiment_setup() -> void:
	if experiment_setup == null:
		return
	if not experiment_setup.visible:
		pause_before_setup = sim.paused
	planet_picker.select(sim.planet_index)
	founder_slider.value = 1
	_on_founder_count_changed(1.0)
	seed_input.clear()
	_update_experiment_summary(sim.planet_index)
	experiment_setup.show()
	resume_experiment_button.visible = experiment_started
	setup_message.text = ""
	sim.paused = true
	_refresh_ui()

func _show_galaxy() -> void:
	if not experiment_setup.visible:
		_show_experiment_setup()
	preload("res://scripts/ui_manager.gd").show_galaxy(self, planet_picker.selected, func(index: int) -> void:
		planet_picker.select(index)
		_update_experiment_summary(index)
		_refresh_ui()
	, sim.planet_overrides)

func _show_landing_map() -> void:
	preload("res://scripts/ui_manager.gd").show_landing(self, planet_picker.selected, sector_picker.selected - 1, func(index: int) -> void:
		sector_picker.select(index + 1)
		_refresh_ui()
	, sim.planet_data(planet_picker.selected))

func _show_planet_conditions(index: int) -> void:
	preload("res://scripts/ui_manager.gd").show_planet_settings(self, sim, index, func() -> void:
		var sector := sector_picker.selected
		_update_experiment_summary(planet_picker.selected)
		sector_picker.select(clampi(sector, 0, sector_picker.item_count - 1))
		_refresh_ui()
	)

func _begin_experiment() -> void:
	var experiment_seed := -1
	var seed_text := seed_input.text.strip_edges()
	if not seed_text.is_empty():
		if not seed_text.is_valid_int() or int(seed_text) < 0:
			setup_message.text = "Enter a non-negative integer seed or leave the field blank."
			return
		experiment_seed = int(seed_text)
	var founding_sector := -1 if sector_picker.selected == 0 else sector_picker.selected - 1
	sim.seed_world(roundi(founder_slider.value), planet_picker.selected, experiment_seed, founding_sector)
	experiment_started = true
	autosave_timer = 0.0
	sim.paused = false
	experiment_setup.hide()
	_refresh_ui()

func _return_to_experiment() -> void:
	if not experiment_started:
		return
	experiment_setup.hide()
	sim.paused = pause_before_setup
	_refresh_ui()

func _load_from_setup(path: String = Save.SAVE_PATH) -> void:
	_request_load(path)

func _on_next_planet() -> void:
	sim.select_planet(sim.planet_index + 1)
	_refresh_ui()

func _on_trigger_event() -> void:
	preload("res://scripts/ui_manager.gd").show_events(self, sim)

func _on_save() -> void:
	_pending_save = true
	_file_status("Подготовка снимка эксперимента…")

func _on_load() -> void:
	preload("res://scripts/ui_manager.gd").show_archive(self, _request_load)

func _request_load(path: String) -> void:
	_pending_load_path = path
	_file_status("Подготовка загрузки эксперимента…")

func _file_status(message: String) -> void:
	_file_message = message
	status_label.tooltip_text = message
	_file_message_until = Time.get_ticks_msec() + 5000
	_refresh_ui()

func _process_file_requests() -> bool:
	if not _pending_save and _pending_load_path.is_empty(): return false
	if sim._step_active:
		sim._process_step_chunk(256, 4000)
		if sim._step_index >= sim.organisms.size():
			sim._finish_step()
			sim._tick_accumulator = maxf(0.0, sim._tick_accumulator - Simulation.TICK_SECONDS)
		return true
	if _pending_save:
		_pending_save = false
		var result := preload("res://scripts/experiment_archive.gd").save_snapshot(sim)
		_file_status("Снимок эксперимента сохранён" if int(result.error) == OK else ("Снимок создан, быстрый слот не обновлён" if not str(result.path).is_empty() else "Не удалось сохранить эксперимент"))
		return true
	var path := _pending_load_path
	_pending_load_path = ""
	var error := Save.load_simulation(sim, path)
	if error == OK:
		experiment_started = true
		autosave_timer = 0.0
		if experiment_setup.visible: sim.paused = false
		experiment_setup.hide()
		_file_status("Эксперимент загружен")
	else:
		var message := "Сохранение не найдено" if error == ERR_FILE_NOT_FOUND else "Не удалось загрузить: файл повреждён или недоступен"
		setup_message.text = message
		_file_status(message)
	return true

func _show_history() -> void:
	preload("res://scripts/ui_manager.gd").show_history(self,sim)

func _show_statistics() -> void:
	var window := Window.new()
	window.title = "ECOSYSTEM STATISTICS"
	window.size = Vector2i(760, 520)
	window.min_size = Vector2i(600, 400)
	var chart := Control.new()
	chart.set_script(Statistics)
	chart.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	chart.set("series", sim.statistics_history.duplicate(true))
	chart.set("death_causes", sim.death_causes.duplicate(true))
	window.add_child(chart)
	add_child(window)
	window.close_requested.connect(window.queue_free)
	RU.apply_tree(window)
	window.popup_centered()

func _show_lineage() -> void:
	preload("res://scripts/ui_manager.gd").show_species(self, sim)

func _show_genome() -> void:
	preload("res://scripts/ui_manager.gd").show_genome(self, sim.selected(), sim)
func _input(event: InputEvent) -> void:
	if experiment_setup != null and experiment_setup.visible:
		if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
			_return_to_experiment()
		return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_SPACE:
			_on_pause()
		elif event.keycode == KEY_EQUAL or event.keycode == KEY_KP_ADD:
			_on_speed()



