class_name EvolutionHistoryView
extends VBoxContainer

signal refresh_requested

const RU = preload("res://scripts/localization_ru.gd")
const PAGE_SIZE := 100
var history: Array[Dictionary] = []
var matches: Array[int] = []
var page := 0
var search: LineEdit
var category: OptionButton
var entries: Tree
var details: RichTextLabel
var count_label: Label
var page_label: Label
var previous_button: Button
var next_button: Button

func _ready() -> void:
	add_theme_constant_override("separation",10)
	var filters := HBoxContainer.new()
	filters.add_theme_constant_override("separation",8)
	add_child(filters)
	search = LineEdit.new()
	search.placeholder_text = "Поиск по событиям и видам"
	search.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	filters.add_child(search)
	category = OptionButton.new()
	for label in ["Все события","Виды","События среды","Эксперимент"]: category.add_item(label)
	filters.add_child(category)
	var refresh := Button.new()
	refresh.text = "Обновить"
	refresh.tooltip_text = "Получить историю текущего эксперимента"
	filters.add_child(refresh)
	refresh.pressed.connect(func() -> void: refresh_requested.emit())
	search.text_changed.connect(func(_value: String) -> void: _apply_filter())
	category.item_selected.connect(func(_index: int) -> void: _apply_filter())
	count_label = Label.new()
	add_child(count_label)
	entries = Tree.new()
	entries.columns = 3
	entries.hide_root = true
	entries.column_titles_visible = true
	entries.set_column_title(0,"Млн лет")
	entries.set_column_title(1,"Событие")
	entries.set_column_title(2,"Подробности")
	entries.set_column_expand(0,false)
	entries.set_column_custom_minimum_width(0,100)
	entries.size_flags_vertical = Control.SIZE_EXPAND_FILL
	entries.custom_minimum_size.y = 150
	entries.item_selected.connect(_show_selected)
	add_child(entries)
	details = RichTextLabel.new()
	details.custom_minimum_size.y = 90
	add_child(details)
	var navigation := HBoxContainer.new()
	navigation.add_theme_constant_override("separation",12)
	add_child(navigation)
	previous_button = Button.new()
	previous_button.text = "← Новее"
	previous_button.pressed.connect(func() -> void: _change_page(-1))
	navigation.add_child(previous_button)
	page_label = Label.new()
	page_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	page_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	navigation.add_child(page_label)
	next_button = Button.new()
	next_button.text = "Старее →"
	next_button.pressed.connect(func() -> void: _change_page(1))
	navigation.add_child(next_button)
	_apply_filter()

func set_history(records: Array[Dictionary]) -> void:
	history.assign(records.duplicate(true))
	if is_node_ready(): _apply_filter()

func _apply_filter() -> void:
	matches.clear()
	var query := search.text.strip_edges().to_lower()
	for index in range(history.size() - 1,-1,-1):
		var record: Dictionary = history[index]
		var name := str(record.get("name",""))
		var group := 1 if name in ["New species detected","Species extinction"] else (2 if name in EnvironmentSystem.EVENTS else 3)
		if category.selected != 0 and category.selected != group: continue
		var searchable := RU.text(name + " " + str(record.get("detail","")) + " " + str(record.get("ancestor",""))).to_lower()
		if not query.is_empty() and query not in searchable: continue
		matches.append(index)
	page = 0
	_render_page()

func _change_page(delta: int) -> void:
	page = clampi(page + delta,0,maxi(0,ceili(float(matches.size()) / PAGE_SIZE) - 1))
	_render_page()

func _render_page() -> void:
	entries.clear()
	var root := entries.create_item()
	for offset in range(page * PAGE_SIZE,mini((page + 1) * PAGE_SIZE,matches.size())):
		var index := matches[offset]
		var record: Dictionary = history[index]
		var item := entries.create_item(root)
		item.set_metadata(0,index)
		item.set_text(0,"%.2f" % (float(record.get("time",0.0)) / 10.0))
		item.set_text(1,RU.text(str(record.get("name","Событие"))))
		item.set_text(2,RU.text(str(record.get("detail",""))))
		item.set_tooltip_text(2,item.get_text(2))
	count_label.text = "Найдено %d из %d событий · сначала новые" % [matches.size(),history.size()]
	page_label.text = "Страница %d из %d" % [page + 1,maxi(1,ceili(float(matches.size()) / PAGE_SIZE))]
	previous_button.disabled = page == 0
	next_button.disabled = (page + 1) * PAGE_SIZE >= matches.size()
	details.text = "Выберите событие для просмотра подробностей." if not matches.is_empty() else "Событий по выбранному фильтру нет."

func _show_selected() -> void:
	var item := entries.get_selected()
	if item == null: return
	var record: Dictionary = history[int(item.get_metadata(0))]
	details.text = "%s · %.2f млн лет\n%s" % [RU.text(str(record.get("name","Событие"))),float(record.get("time",0.0)) / 10.0,RU.text(str(record.get("detail","")))]
	if record.has("ancestor"): details.text += "\nРодительский вид: " + RU.text(str(record.ancestor))
	if record.has("duration"): details.text += "\nПродолжительность: %.2f млн лет" % (float(record.duration) / 10.0)
