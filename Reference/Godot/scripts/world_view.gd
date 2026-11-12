class_name WorldView
extends Control

const RU = preload("res://scripts/localization_ru.gd")

var sim: SimulationManager
var zoom_level := 1.0
var camera_pan := Vector2.ZERO
var _dragging := false
var _last_mouse := Vector2.ZERO
var _terrain_planet := -1
var _terrain_generation := 0
var _terrain_textures: Dictionary = {}
var _terrain_ready: Dictionary = {}

const GOLD := Color("ffd18a")
const CYAN := Color("54d7d2")

func _ready() -> void:
	clip_contents = true
	mouse_filter = Control.MOUSE_FILTER_STOP
	gui_input.connect(_on_gui_input)

func _process(_delta: float) -> void:
	queue_redraw()

func _draw() -> void:
	if sim == null:
		return
	draw_rect(Rect2(Vector2.ZERO, size), Color("09243a"))
	var origin := sim.sim_bounds.position
	var center := size * 0.5
	var transform_offset := center * (1.0 - zoom_level) - camera_pan * zoom_level
	draw_set_transform(transform_offset, 0.0, Vector2.ONE * zoom_level)
	var biomes: Array = BiomeManager.biomes_for_planet(sim.planet_index)
	if _terrain_planet != sim.planet_index:
		_terrain_textures.clear()
		_terrain_ready.clear()
		_terrain_planet = sim.planet_index
		_terrain_generation += 1
		for biome in biomes:
			var name := str(biome.name)
			var requested_generation := _terrain_generation
			var texture := preload("res://scripts/biome_appearance.gd").texture(biome,sim.planet_index)
			_terrain_textures[name] = texture
			texture.changed.connect(func() -> void:
				if _terrain_generation != requested_generation: return
				_terrain_ready[name] = true
				queue_redraw()
			)
	for x in range(4):
		for y in range(3):
			var biome: Dictionary = biomes[posmod(x + y * 2, biomes.size())]
			var rect := Rect2(x * size.x / 4.0, y * size.y / 3.0, size.x / 4.0 + 1.0, size.y / 3.0 + 1.0)
			draw_rect(rect, Color(str(biome.color)))
			if _terrain_ready.get(str(biome.name),false): draw_texture_rect(_terrain_textures[str(biome.name)],rect,false)
			draw_rect(rect, Color(CYAN, 0.05), false, 1)
	for i in range(15):
		var x := fposmod(float(i) * 67.0 + fposmod(Time.get_ticks_msec() * 0.012, 67.0), size.x)
		var y := fposmod(float(i * 91) + sin(Time.get_ticks_msec() * 0.0005 + i) * 24.0, size.y)
		draw_circle(Vector2(x, y), 1.5 + fposmod(i, 3.0), Color(0.48, 0.85, 0.9, 0.22))
	for resource_index in range(sim.food.size()):
		var point: Vector2 = sim.food[resource_index]
		if point.x >= origin.x:
			var organic := sim.resource_kind(resource_index) == "carcass"
			var color := Color("c49a77") if organic else GOLD
			draw_circle(point - origin, 5.0, Color(color, 0.11))
			draw_circle(point - origin, 2.8 if organic else 2.0, color)
	_draw_cells(origin)
	draw_rect(Rect2(Vector2.ZERO, size), Color(CYAN, 0.18), false, 1)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
	draw_string(ThemeDB.fallback_font, Vector2(14, 20), RU.text("NUTRIENT CURRENT  ·  %s") % RU.text(BiomeManager.at_position(sim.sim_bounds.get_center(), sim.sim_bounds, sim.planet_index).name.to_upper()), HORIZONTAL_ALIGNMENT_LEFT, size.x - 28, 9, Color(RU.text("d6e7ef")))
	draw_string(ThemeDB.fallback_font, Vector2(14, size.y - 14), RU.text("WHEEL ZOOM  ·  MIDDLE DRAG PAN  ·  %.1f×") % zoom_level, HORIZONTAL_ALIGNMENT_LEFT, size.x - 28, 8, Color(RU.text("b6ccd6")))

func _draw_cells(origin: Vector2) -> void:
	var stride := maxi(1, ceili(float(sim.organisms.size()) / 1200.0))
	for organism_index in range(sim.organisms.size()):
		if stride > 1 and organism_index % stride != 0 and int(sim.organisms[organism_index].id) != sim._selected_id:
			continue
		var o: Dictionary = sim.organisms[organism_index]
		var genes: Dictionary = o.genes
		var radius := 2.0 + float(genes.size) * 2.1 + float(genes.coloniality) * 2.0
		var body_color := Color.from_hsv(float(abs(str(o.species).hash()) % 360) / 360.0, 0.58, 0.95)
		match str(o.kind):
			"Producer": body_color = Color("76e6a2")
			"Grazer": body_color = Color("ffd18a")
			"Predator": body_color = Color("ff6f88")
			"Omnivore": body_color = Color("ffad73")
			"Scavenger": body_color = Color("c49a77")
			"Decomposer": body_color = Color("aa9a72")
			"Colony": body_color = Color("c7a1ff")
		var p: Vector2 = o.pos - origin
		if int(o.id) == sim._selected_id:
			draw_arc(p, radius + 5.0, 0, TAU, 24, Color.WHITE, 1.2)
		draw_circle(p, radius + 2.5 + float(genes.luminescence) * 6.0, Color(body_color, 0.10 + float(genes.luminescence) * 0.25))
		draw_circle(p, radius, body_color)
		if zoom_level >= 0.8:
			var nucleus_offset := Vector2(cos(float(o.heading)),sin(float(o.heading))) * radius * -0.2
			draw_circle(p + nucleus_offset,radius * 0.28,Color("b8a9df"))
			draw_circle(p + nucleus_offset + Vector2(-0.25,-0.25),radius * 0.11,Color("e7e3f3"))
		var body: Dictionary = o.get("body", {})
		var roles: Array = body.get("roles", [])
		if roles.size() > 1:
			for cell_index in range(roles.size()):
				var cell_angle := float(o.heading) + float(cell_index) * TAU / float(roles.size())
				var cell_position := p + Vector2(cos(cell_angle), sin(cell_angle)) * radius * 0.8
				var cell_color := body_color
				match str(roles[cell_index]):
					"feeding": cell_color = Color("76e6a2")
					"motor": cell_color = Color("54d7d2")
					"defense": cell_color = Color("d6e7ef")
					"germline": cell_color = Color("ffd18a")
				draw_circle(cell_position, radius * 0.45, Color(cell_color, 0.85))
		var forward := Vector2(cos(float(o.heading)), sin(float(o.heading)))
		var sideways := forward.orthogonal()
		var fins := float(genes.get("fins", 0.0))
		if fins > 0.15:
			for side in [-1.0, 1.0]:
				var fin_points := PackedVector2Array([p + forward * radius * 0.5, p + sideways * side * radius * (1.0 + fins) - forward * radius * 0.4, p - forward * radius * 0.7])
				draw_colored_polygon(fin_points, Color(body_color, 0.65))
		var flagella := float(genes.get("flagella", 0.1))
		if flagella > 0.15:
			var tail := PackedVector2Array()
			for segment in range(7):
				var offset := float(segment) / 6.0
				var wave := sin(offset * TAU + Time.get_ticks_msec() * 0.008 + float(o.id))
				tail.append(p - forward * radius * (1.0 + offset * flagella * 3.0) + sideways * wave * offset * radius * 0.4)
			draw_polyline(tail, Color(body_color, 0.8), 1.0)
		if int(o.id) == sim._selected_id:
			var health_ratio := float(o.get("health", 1.0)) / OrganismManager.max_health(genes)
			draw_line(p + Vector2(-6, radius + 9), p + Vector2(6, radius + 9), Color("38404b"), 2.0)
			draw_line(p + Vector2(-6, radius + 9), p + Vector2(-6 + 12 * health_ratio, radius + 9), Color("76e6a2"), 2.0)
		if float(genes.armor) > 0.2:
			draw_arc(p, radius + 1.0, 0, TAU, 12, Color(0.88, 0.92, 1.0, minf(0.95, float(genes.armor))), 1.0 + float(genes.armor))
		var appendages := 1 + roundi(float(genes.cilia) * 5.0 + float(genes.limbs) * 3.0)
		for appendage in range(appendages):
			var angle: float = float(o.heading) + float(appendage) * TAU / float(appendages) + 1.2
			var tip := p + Vector2(cos(angle), sin(angle)) * radius * (1.35 + float(genes.limbs) * 0.6)
			draw_line(p, tip, Color(body_color, 0.35 + float(genes.cilia) * 0.55), 0.8 + float(genes.limbs))

func _on_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_MIDDLE:
			_dragging = event.pressed
			_last_mouse = event.position
		elif event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_UP:
			_zoom_at(event.position, 1.15)
		elif event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			_zoom_at(event.position, 1.0 / 1.15)
		elif event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
			var center := size * 0.5
			var local_world: Vector2 = center + (event.position - center) / zoom_level + camera_pan
			sim.select_at(local_world + sim.sim_bounds.position)
	elif event is InputEventMouseMotion and _dragging:
		camera_pan -= (event.position - _last_mouse) / zoom_level
		_last_mouse = event.position
		queue_redraw()
	accept_event()

func _zoom_at(point: Vector2, factor: float) -> void:
	var old_zoom := zoom_level
	var new_zoom := clampf(zoom_level * factor, 0.55, 3.5)
	var center := size * 0.5
	var world_point := center + (point - center) / old_zoom + camera_pan
	zoom_level = new_zoom
	camera_pan = world_point - center - (point - center) / new_zoom
	queue_redraw()

