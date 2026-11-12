extends RefCounted

const RU = preload("res://scripts/localization_ru.gd")
const Multicellular = preload("res://scripts/multicellular_system.gd")

static func describe(partial: Dictionary) -> String:
	if partial.is_empty(): return "Адаптации не записаны в этом сохранении."
	var genes := GeneticsSystem.complete_genome(partial)
	var body := Multicellular.develop(genes)
	var lines: Array[String] = ["АДАПТАЦИИ И ИХ ДЕЙСТВИЕ", "Пищевая роль: " + RU.text(GeneticsSystem.trophic_role(genes))]
	lines.append("Питание без стресса: минеральная частица — %.2f энергии; органический запас 8 — %.2f." % [EcosystemSystem.resource_energy(genes,"nutrient",8.0,body.feeding_multiplier),EcosystemSystem.resource_energy(genes,"carcass",8.0,body.feeding_multiplier)])
	if float(genes.phototrophy) > 0.25: lines.append("Фотосинтез: получает энергию от доступного света; затенение снижает этот приток.")
	if float(genes.chemotrophy) > 0.25: lines.append("Хемосинтез: получает энергию от геологической активности планеты и биома.")
	if float(genes.predation) > 0.55: lines.append("Охота: ищет подходящих меньших жертв; защита и размеры жертвы влияют на успех.")
	elif float(genes.herbivory) > 0.65: lines.append("Выпас: ищет организмы-производители, включая колониальные формы.")
	if maxf(float(genes.scavenging),float(genes.decomposition)) > 0.25: lines.append("Органическое питание: повышает предпочтение и усвоение останков. Их запас конечен.")
	if maxf(maxf(float(genes.predation),float(genes.herbivory)),maxf(float(genes.scavenging),float(genes.decomposition))) > 0.6: lines.append("Цена пищевой специализации: снижается усвоение минеральных частиц.")
	if float(genes.armor) > 0.2: lines.append("Защитный покров: уменьшает повреждения и обезвоживание.")
	if float(genes.resilience) > 0.3: lines.append("Устойчивость: увеличивает запас здоровья, уменьшает повреждения и действие токсинов.")
	lines.append("Температурная, радиационная и барическая устойчивости смягчают соответствующий стресс окружающей среды.")
	if float(genes.gills) > 0.3: lines.append("Дыхание: лучше использует доступный кислород и уменьшает кислородный стресс.")
	if float(genes.eyes) > 0.3: lines.append("Зрение: увеличивает дальность обнаружения пищи и организмов.")
	if float(genes.luminescence) > 0.1: lines.append("Биолюминесценция: помогает поиску в темноте, постоянно расходуя энергию.")
	if maxf(maxf(float(genes.cilia),float(genes.limbs)),maxf(float(genes.flagella),float(genes.fins))) > 0.3: lines.append("Органы движения: изменяют реальную скорость; вода и гравитация также влияют на движение.")
	if int(body.cell_count) > 1:
		lines.append("Тело из %d клеток: меняет питание, движение и запас здоровья, увеличивая расходы на поддержание и рождение." % int(body.cell_count))
		if float(genes.differentiation) >= 0.5: lines.append("Специализация клеток: питательные, двигательные и защитные ткани выполняют разные функции.")
	if float(genes.sexual_reproduction) >= 0.6: lines.append("Половое размножение: нужен совместимый зрелый партнёр; гены смешиваются, энергию передают оба родителя.")
	else: lines.append("Деление: потомок получает наследственный геном с возможными мутациями и энергию родителя.")
	lines.append("Выживаемость зависит от условий и ресурсов. Наличие признака не гарантирует преимущество во всех биомах.")
	return "\n".join(lines)
