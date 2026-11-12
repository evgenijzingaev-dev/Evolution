class_name StatisticsView
extends Control

const RU = preload("res://scripts/localization_ru.gd")

var series: Array = []
var death_causes: Dictionary = {}
const GRID := Color("284451")
const TEXT := Color("d6e7ef")
const MUTED := Color("7f9baa")
const SERIES := {"population": Color("54d7d2"), "species": Color("e9eef5"), "producers": Color("76e6a2"), "grazers": Color("ffd18a"), "predators": Color("ff6f88"), "decomposers": Color("c7a1ff"), "omnivores": Color("ffaa65"), "scavengers": Color("ba976c")}

func _draw() -> void:
	var bounds := Rect2(Vector2(68, 102), size - Vector2(100, 172))
	draw_rect(Rect2(Vector2.ZERO, size), Color("091824"))
	var deaths_text := "Смерти: голод %d · возраст %d · среда %d · хищники %d" % [int(death_causes.get("starvation", 0)), int(death_causes.get("age", 0)), int(death_causes.get("environment", 0)), int(death_causes.get("predation", 0))]
	draw_string(ThemeDB.fallback_font, Vector2(20, size.y - 12), deaths_text, HORIZONTAL_ALIGNMENT_LEFT, size.x - 40, 11, TEXT)
	for i in range(5):
		var y := bounds.position.y + bounds.size.y * float(i) / 4.0
		draw_line(Vector2(bounds.position.x, y), Vector2(bounds.end.x, y), GRID, 1.0)
	for i in range(7):
		var x := bounds.position.x + bounds.size.x * float(i) / 6.0
		draw_line(Vector2(x, bounds.position.y), Vector2(x, bounds.end.y), Color(GRID, 0.6), 1.0)
	draw_string(ThemeDB.fallback_font, Vector2(20, 27), RU.text("POPULATION AND TROPHIC LEVELS"), HORIZONTAL_ALIGNMENT_LEFT, -1, 12, TEXT)
	draw_string(ThemeDB.fallback_font, Vector2(70, size.y - 36), RU.text("EARLIER"), HORIZONTAL_ALIGNMENT_LEFT, -1, 9, MUTED)
	draw_string(ThemeDB.fallback_font, Vector2(bounds.end.x - 80, size.y - 36), RU.text("NOW"), HORIZONTAL_ALIGNMENT_RIGHT, 80, 9, MUTED)
	if series.size() < 2:
		draw_string(ThemeDB.fallback_font, bounds.get_center(), RU.text("Collecting observations…"), HORIZONTAL_ALIGNMENT_CENTER, 250, 14, MUTED)
		return
	var max_population := 1.0
	for point in series:
		for key in SERIES:
			max_population = maxf(max_population, float(point.get(key, 0.0)))
	var step_x := bounds.size.x / float(series.size() - 1)
	var keys := SERIES.keys()
	for key_index in range(keys.size()):
		var key: String = keys[key_index]
		var points := PackedVector2Array()
		for i in range(series.size()):
			var point: Dictionary = series[i]
			var x := bounds.position.x + float(i) * step_x
			var value := float(point.get(key, 0.0))
			points.append(Vector2(x, bounds.end.y - value / max_population * bounds.size.y))
		draw_polyline(points, SERIES[key], 2.5 if key == "population" else 1.4, true)
		var column_width := (size.x - 40.0) / 4.0
		var legend_pos := Vector2(20 + (key_index % 4) * column_width, 51 + (key_index / 4) * 22)
		draw_line(legend_pos, legend_pos + Vector2(16, 0), SERIES[key], 2.0)
		draw_string(ThemeDB.fallback_font, legend_pos + Vector2(22, 4), RU.text(key.capitalize()), HORIZONTAL_ALIGNMENT_LEFT, column_width - 25, 11, TEXT)
	draw_string(ThemeDB.fallback_font, Vector2(12, bounds.position.y + 4), str(int(ceil(max_population))), HORIZONTAL_ALIGNMENT_LEFT, 50, 10, MUTED)
	draw_string(ThemeDB.fallback_font, Vector2(12, bounds.end.y), "0", HORIZONTAL_ALIGNMENT_LEFT, 50, 10, MUTED)

