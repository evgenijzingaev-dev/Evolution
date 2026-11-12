class_name BiomeManager
extends RefCounted

const PALETTES := [
	[
		{"name":"Kelp forest","color":"0a4651","temperature":-2.0,"pressure":1.0,"oxygen":1.2,"light":0.86,"food":1.3,"geology":0.12},
		{"name":"Coral shelf","color":"12516a","temperature":3.0,"pressure":1.0,"oxygen":1.15,"light":1.0,"food":1.4,"geology":0.15},
		{"name":"Open ocean","color":"0b3455","temperature":-4.0,"pressure":1.2,"oxygen":0.9,"light":0.52,"food":0.88,"geology":0.1},
		{"name":"Abyssal plain","color":"101f40","temperature":-17.0,"pressure":3.4,"oxygen":0.55,"light":0.03,"food":0.58,"geology":0.22},
		{"name":"Tidal marsh","color":"24564d","temperature":5.0,"pressure":1.0,"oxygen":1.4,"light":0.9,"food":1.5,"geology":0.2},
		{"name":"Hydrothermal vent","color":"3b3049","temperature":34.0,"pressure":4.0,"oxygen":0.24,"light":0.0,"food":1.05,"geology":1.5}
	],
	[
		{"name":"Salt basin","color":"775335","temperature":21.0,"pressure":0.82,"oxygen":0.38,"light":1.35,"food":0.55,"geology":0.15},
		{"name":"Glass desert","color":"946741","temperature":33.0,"pressure":0.7,"oxygen":0.12,"light":1.55,"food":0.35,"geology":0.1},
		{"name":"Oasis","humidity":0.85,"water":0.7,"radiation_multiplier":0.35,"color":"24665c","temperature":7.0,"pressure":0.9,"oxygen":0.8,"light":1.2,"food":1.15,"geology":0.3},
		{"name":"Basalt canyon","color":"483f4b","temperature":16.0,"pressure":0.95,"oxygen":0.2,"light":0.68,"food":0.62,"geology":0.56}
	],
	[
		{"name":"Ice shelf","color":"75919b","temperature":-8.0,"pressure":1.5,"oxygen":0.8,"light":0.18,"food":0.55,"geology":0.12},
		{"name":"Subglacial sea","color":"1c516a","temperature":4.0,"pressure":2.5,"oxygen":0.55,"light":0.03,"food":0.75,"geology":0.18},
		{"name":"Brine lake","color":"406b79","temperature":-17.0,"pressure":1.1,"oxygen":0.38,"light":0.12,"food":0.46,"geology":0.12},
		{"name":"Warm vent","color":"594c61","temperature":29.0,"pressure":4.2,"oxygen":0.15,"light":0.0,"food":1.0,"geology":1.45}
	],
	[
		{"name":"Lava delta","color":"623d38","temperature":28.0,"pressure":3.0,"oxygen":0.08,"light":0.55,"food":0.65,"geology":1.6},
		{"name":"Sulfur pool","color":"826943","temperature":19.0,"pressure":4.0,"oxygen":0.02,"light":0.32,"food":0.82,"geology":1.8},
		{"name":"Ash desert","color":"574b50","temperature":8.0,"pressure":1.6,"oxygen":0.12,"light":0.85,"food":0.4,"geology":0.58},
		{"name":"Caldera lake","humidity":0.7,"water":0.75,"toxicity_multiplier":0.25,"color":"274d59","temperature":-35.0,"pressure":2.2,"oxygen":0.24,"light":0.4,"food":0.78,"geology":1.1}
	],
	[
		{"name":"Twilight zone","color":"183f63","temperature":-5.0,"pressure":0.3,"oxygen":0.35,"light":0.12,"food":0.68,"geology":0.16},
		{"name":"Abyssal trench","color":"111d3e","temperature":-12.0,"pressure":1.0,"oxygen":0.2,"light":0.0,"food":0.48,"geology":0.45},
		{"name":"Pelagic drift","color":"13516a","temperature":2.0,"pressure":0.1,"oxygen":0.85,"light":0.56,"food":1.0,"geology":0.22},
		{"name":"Hydrothermal vent","color":"423449","temperature":31.0,"pressure":0.65,"oxygen":0.12,"light":0.0,"food":0.92,"geology":1.7}
	]
]

static func biomes_for_planet(planet_index: int) -> Array:
	return PALETTES[posmod(planet_index, PALETTES.size())].duplicate(true)

static func at_position(position: Vector2, bounds: Rect2, planet_index: int) -> Dictionary:
	var cell_x := clampi(int((position.x - bounds.position.x) / bounds.size.x * 4.0), 0, 3)
	var cell_y := clampi(int((position.y - bounds.position.y) / bounds.size.y * 3.0), 0, 2)
	var biomes: Array = PALETTES[posmod(planet_index, PALETTES.size())]
	return biomes[posmod(cell_x + cell_y * 2, biomes.size())]


