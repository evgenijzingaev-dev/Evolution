extends Control

signal selection_changed(index: int)

var selected_planet := 0
var planet_overrides: Dictionary = {}
const RU = preload("res://scripts/localization_ru.gd")
const COLORS := [Color("54d7d2"), Color("dc9c64"), Color("acdaf2"), Color("ed725a"), Color("4f90ec")]

func _ready() -> void:
	custom_minimum_size = Vector2(700, 330)
	mouse_filter = Control.MOUSE_FILTER_STOP
	focus_mode = Control.FOCUS_ALL

func planet_position(index: int) -> Vector2:
	return Vector2(size.x * (0.12 + index * 0.19), size.y * (0.38 if index % 2 == 0 else 0.57))

func choose(index: int) -> void:
	selected_planet = clampi(index, 0, 4)
	selection_changed.emit(selected_planet)
	queue_redraw()

func description(index: int) -> String:
	var planet := PlanetManager.get_planet(index)
	planet.merge(planet_overrides.get(str(index), {}), true)
	return "%s · %s\nТемпература %.1f °C · давление %.2f бар · вода %.0f%% · радиация %.2f" % [RU.text(str(planet.name)), RU.text(str(planet.biome)), planet.temperature, planet.pressure, float(planet.water) * 100.0, planet.radiation]

func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color("071522"))
	for i in range(90):
		var star := Vector2(fposmod(i * 137.3, size.x), fposmod(i * 79.7, size.y))
		draw_circle(star, 0.8 + float(i % 3) * 0.3, Color(0.6, 0.85, 1.0, 0.25 + float(i % 4) * 0.1))
	for index in range(5):
		var point := planet_position(index)
		var selected := index == selected_planet
		draw_circle(point, 41, Color(COLORS[index], 0.08))
		draw_circle(point, 28, COLORS[index])
		draw_circle(point + Vector2(10, -3), 23, Color(0.02, 0.06, 0.1, 0.5))
		if selected: draw_arc(point, 37, 0, TAU, 50, Color("ffd18a"), 2.0)
		draw_string(ThemeDB.fallback_font, point + Vector2(-55, 65), RU.text(str(PlanetManager.get_planet(index).name)), HORIZONTAL_ALIGNMENT_CENTER, 110, 16, Color("edffff"))

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		for index in range(5):
			if event.position.distance_to(planet_position(index)) <= 42.0:
				choose(index)
				grab_focus()
				accept_event()
				break
	elif event is InputEventKey and event.pressed and event.keycode in [KEY_LEFT, KEY_RIGHT]:
		choose(posmod(selected_planet + (-1 if event.keycode == KEY_LEFT else 1), 5))
		accept_event()
