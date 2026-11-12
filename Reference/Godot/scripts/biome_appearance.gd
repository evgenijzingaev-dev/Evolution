extends RefCounted

static func texture(biome: Dictionary,planet_index: int) -> NoiseTexture2D:
	var base := Color(str(biome.color))
	var gradient := Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0,0.5,1.0])
	gradient.colors = PackedColorArray([base.darkened(0.22),base,base.lightened(0.025)])
	var noise := FastNoiseLite.new()
	noise.seed = int(str(biome.name).hash() % 1000000) + planet_index * 173
	noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	noise.frequency = 0.014
	noise.fractal_octaves = 2
	var result := NoiseTexture2D.new()
	result.width = 256
	result.height = 256
	result.seamless = true
	result.generate_mipmaps = false
	result.color_ramp = gradient
	result.noise = noise
	return result
