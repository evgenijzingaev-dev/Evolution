class_name PlanetManager
extends RefCounted

const PARAMETER_RANGES := {"temperature":[-100.0,150.0],"pressure":[0.1,100.0],"radiation":[0.0,5.0],"oxygen":[0.0,15.0],"humidity":[0.0,1.0],"gravity":[0.1,3.0],"water":[0.0,1.0],"sunlight":[0.0,2.0],"geology":[0.0,2.0],"toxicity":[0.0,1.0],"food_rate":[0.0,3.0]}

const PLANETS := [
	{"name":"Terra", "biome":"PELAGIC ZONE", "temperature":18.4, "pressure":1.02, "radiation":0.14, "oxygen":7.8, "humidity":0.78, "gravity":1.0, "water":0.72, "sunlight":0.82, "geology":0.18, "temp_tolerance":1.0, "radiation_tolerance":1.0, "pressure_tolerance":1.0, "food_rate":1.0},
	{"name":"Arrox", "biome":"SCORCHED BASIN", "temperature":47.0, "pressure":0.72, "radiation":2.8, "oxygen":0.3, "humidity":0.08, "gravity":0.86, "water":0.09, "sunlight":1.35, "geology":0.12, "temp_tolerance":1.55, "radiation_tolerance":2.1, "pressure_tolerance":0.8, "food_rate":0.65},
	{"name":"Nivea", "biome":"SUBGLACIAL SEA", "temperature":-24.0, "pressure":3.2, "radiation":0.05, "oxygen":1.2, "humidity":0.92, "gravity":1.15, "water":0.88, "sunlight":0.12, "geology":0.34, "temp_tolerance":1.8, "radiation_tolerance":0.8, "pressure_tolerance":1.8, "food_rate":0.62},
	{"name":"Ignis", "biome":"VOLCANIC VENT", "temperature":81.0, "pressure":4.8, "radiation":0.5, "oxygen":0.1, "humidity":0.16, "gravity":1.28, "water":0.22, "sunlight":0.74, "geology":0.94, "temp_tolerance":2.2, "radiation_tolerance":1.2, "pressure_tolerance":2.4, "food_rate":0.82},
	{"name":"Thalassa", "biome":"ABYSSAL OCEAN", "temperature":3.0, "pressure":42.0, "radiation":0.08, "oxygen":2.1, "humidity":1.0, "gravity":0.94, "water":1.0, "sunlight":0.65, "geology":0.42, "temp_tolerance":1.4, "radiation_tolerance":0.8, "pressure_tolerance":4.0, "food_rate":0.75}
]

static func get_planet(index: int) -> Dictionary:
	var normalized := posmod(index, PLANETS.size())
	var result: Dictionary = PLANETS[normalized].duplicate(true)
	result["atmosphere"] = ["Азот и кислород", "Разреженная углекислотная", "Плотная азотная", "Углекислый газ и сернистые газы", "Влажная азотная"][normalized]
	result["toxicity"] = [0.0, 0.08, 0.02, 0.3, 0.01][normalized]
	return result

