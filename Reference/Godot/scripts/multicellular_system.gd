extends RefCounted

static func develop(genes: Dictionary) -> Dictionary:
	var coloniality := float(genes.get("coloniality", 0.0))
	var count := 1 if coloniality < 0.35 else clampi(2 + roundi((coloniality - 0.35) * 10.0), 2, 9)
	var differentiated := float(genes.get("differentiation", 0.0)) >= 0.5
	var roles: Array[String] = ["germline"]
	var feeding := 0
	var motor := 0
	var defense := 0
	var weights := [0.2 + float(genes.phototrophy) + float(genes.chemotrophy) + float(genes.herbivory) + float(genes.decomposition), 0.2 + float(genes.cilia) + float(genes.limbs) + float(genes.get("fins", 0.0)), 0.2 + float(genes.armor) + float(genes.get("resilience", 0.1))]
	var allocated := [0, 0, 0]
	for i in range(count - 1):
		if not differentiated:
			roles.append("generalist")
			continue
		var best := 0
		for role in range(1, 3):
			if float(weights[role]) / float(allocated[role] + 1) > float(weights[best]) / float(allocated[best] + 1):
				best = role
		allocated[best] += 1
		roles.append(["feeding", "motor", "defense"][best])
	feeding = allocated[0]
	motor = allocated[1]
	defense = allocated[2]
	return {"cell_count":count,"roles":roles,"feeding_cells":feeding,"motor_cells":motor,"defense_cells":defense,"feeding_multiplier":1.0 + (count - 1) * 0.04 + feeding * 0.1,"movement_multiplier":(1.0 + motor * 0.12) / (1.0 + (count - 1) * 0.05),"health_multiplier":1.0 + (count - 1) * 0.1 + defense * 0.15,"damage_multiplier":1.0 / (1.0 + defense * 0.15),"upkeep":(count - 1) * 0.015 + (feeding + motor + defense) * 0.01}
