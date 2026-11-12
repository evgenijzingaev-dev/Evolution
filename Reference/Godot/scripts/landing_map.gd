extends Control

signal selection_changed(index: int)

var planet_index := 0
var selected_sector := -1
var planet_profile: Dictionary = {}
const RU = preload("res://scripts/localization_ru.gd")

func _ready() -> void:
	custom_minimum_size = Vector2(640, 330)
	mouse_filter = Control.MOUSE_FILTER_STOP
	focus_mode = Control.FOCUS_ALL

func sector_at(point: Vector2) -> int:
	if not Rect2(Vector2.ZERO, size).has_point(point) or size.x <= 0.0 or size.y <= 0.0:
		return -1
	return clampi(int(point.x / size.x * 4.0), 0, 3) + clampi(int(point.y / size.y * 3.0), 0, 2) * 4

func choose(index: int) -> void:
	selected_sector = clampi(index, 0, 11)
	selection_changed.emit(selected_sector)
	queue_redraw()

func description(index: int) -> String:
	if index < 0:
		return "Выберите сектор на карте."
	var biome := _biome(index)
	var planet := PlanetManager.get_planet(planet_index) if planet_profile.is_empty() else planet_profile
	return "Сектор %02d · %s\nТемпература %.1f °C · давление %.2f бар · свет %.0f%% · продуктивность %.0f%%\nВлажность %.0f%% · вода %.0f%% · радиация %.2f" % [index + 1, RU.text(str(biome.name)), float(planet.temperature) + float(biome.temperature), float(planet.pressure) * float(biome.pressure), float(planet.sunlight) * float(biome.light) * 100.0, float(biome.food) * 100.0, float(biome.get("humidity", planet.humidity)) * 100.0, float(biome.get("water", planet.water)) * 100.0, float(planet.radiation) * float(biome.get("radiation_multiplier", 1.0))]

func _biome(index: int) -> Dictionary:
	var biomes := BiomeManager.biomes_for_planet(planet_index)
	return biomes[posmod(index % 4 + (index / 4) * 2, biomes.size())]

func _draw() -> void:
	for index in range(12):
		var tile := Rect2(Vector2(index % 4, index / 4) * size / Vector2(4, 3), size / Vector2(4, 3))
		var biome := _biome(index)
		draw_rect(tile.grow(-2), Color(str(biome.color)))
		draw_rect(tile.grow(-3), Color("ffd18a") if index == selected_sector else Color("41616c"), false, 3.0 if index == selected_sector else 1.0)
		draw_string(ThemeDB.fallback_font, tile.position + Vector2(10, 23), "%02d" % (index + 1), HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color("ffd18a"))
		draw_string(ThemeDB.fallback_font, tile.position + Vector2(10, 48), RU.text(str(biome.name)), HORIZONTAL_ALIGNMENT_LEFT, tile.size.x - 20, 12, Color("edffff"))
		if index == selected_sector:
			draw_circle(tile.get_center() + Vector2(0, 22), 5, Color("ffd18a"))

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var index := sector_at(event.position)
		if index >= 0:
			choose(index)
			grab_focus()
			accept_event()
	elif event is InputEventKey and event.pressed:
		var direction := 0
		match event.keycode:
			KEY_LEFT: direction = -1
			KEY_RIGHT: direction = 1
			KEY_UP: direction = -4
			KEY_DOWN: direction = 4
		if direction != 0:
			choose(clampi(maxi(0, selected_sector) + direction, 0, 11))
			accept_event()
