class_name EnvironmentSystem
extends RefCounted

const EVENTS := ["Drought", "Ice age", "Volcanic eruption", "Meteor impact", "Radiation surge", "Sea level shift", "Atmospheric change", "Climate shift"]

var rng := RandomNumberGenerator.new()
var current_event := ""
var event_remaining := 0.0
var event_duration := 0.0
var temperature_shift := 0.0
var radiation_multiplier := 1.0
var food_multiplier := 1.0
var oxygen_multiplier := 1.0
var humidity_multiplier := 1.0
var sunlight_multiplier := 1.0
var water_shift := 0.0
var toxicity_shift := 0.0
var event_history: Array[Dictionary] = []

func _init() -> void:
	rng.randomize()

func trigger(planet: Dictionary, elapsed: float, requested_event: String = "") -> Dictionary:
	current_event = requested_event if requested_event in EVENTS else EVENTS[rng.randi_range(0, EVENTS.size() - 1)]
	event_duration = rng.randf_range(35.0, 90.0)
	event_remaining = event_duration
	temperature_shift = 0.0
	radiation_multiplier = 1.0
	food_multiplier = 1.0
	oxygen_multiplier = 1.0
	humidity_multiplier = 1.0
	sunlight_multiplier = 1.0
	water_shift = 0.0
	toxicity_shift = 0.0
	match current_event:
		"Drought":
			temperature_shift = 9.0
			food_multiplier = 0.55
			humidity_multiplier = 0.35
			water_shift = -0.12
		"Ice age":
			temperature_shift = -20.0
			food_multiplier = 0.72
		"Volcanic eruption":
			temperature_shift = 15.0
			radiation_multiplier = 1.35
			food_multiplier = 1.35
			toxicity_shift = 0.25
		"Meteor impact":
			food_multiplier = 0.45
			radiation_multiplier = 1.8
			sunlight_multiplier = 0.45
		"Radiation surge":
			radiation_multiplier = 2.8
		"Sea level shift":
			food_multiplier = 0.8 if float(planet.water) < 0.5 else 1.15
			water_shift = -0.15 if float(planet.water) < 0.5 else 0.15
		"Atmospheric change":
			temperature_shift = rng.randf_range(-8.0, 8.0)
			oxygen_multiplier = rng.randf_range(0.35, 1.6)
			toxicity_shift = rng.randf_range(0.0, 0.2)
		"Climate shift":
			temperature_shift = rng.randf_range(-12.0, 12.0)
			humidity_multiplier = rng.randf_range(0.5, 1.5)
	event_history.append({"name": current_event, "time": elapsed, "duration": event_duration})
	if event_history.size() > 64:
		event_history.pop_front()
	return {"name": current_event, "time": elapsed, "duration": event_duration}

func step(dt: float) -> void:
	if event_remaining <= 0.0:
		return
	event_remaining = maxf(0.0, event_remaining - dt)
	if event_remaining == 0.0:
		current_event = ""
		temperature_shift = 0.0
		radiation_multiplier = 1.0
		food_multiplier = 1.0
		oxygen_multiplier = 1.0
		humidity_multiplier = 1.0
		sunlight_multiplier = 1.0
		water_shift = 0.0
		toxicity_shift = 0.0

func event_data() -> Dictionary:
	return {"name": current_event, "remaining": event_remaining, "temperature_shift": temperature_shift, "radiation_multiplier": radiation_multiplier, "food_multiplier": food_multiplier,"oxygen_multiplier":oxygen_multiplier,"humidity_multiplier":humidity_multiplier,"sunlight_multiplier":sunlight_multiplier,"water_shift":water_shift,"toxicity_shift":toxicity_shift}
