extends SceneTree

const History = preload("res://scripts/evolution_history_view.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var sim := SimulationManager.new()
	sim.timeline.clear()
	for index in range(250):
		sim.timeline.append({"name":"New species detected","detail":"branch-%d" % index,"ancestor":"parent","time":float(index)})
	sim.timeline.append({"name":"Drought","time":300.0,"duration":40.0})
	var window := preload("res://scripts/ui_manager.gd").show_history(root,sim)
	await process_frame
	var view: History = window.get_child(0)
	assert(view.matches.size() == 251 and view.entries.get_root().get_child_count() == 100)
	assert(view.entries.get_root().get_first_child().get_text(1) == "Засуха")
	view._change_page(2)
	assert(view.page == 2 and view.entries.get_root().get_child_count() == 51)
	assert(view.next_button.disabled and not view.previous_button.disabled)
	view.category.select(2)
	view._apply_filter()
	assert(view.matches.size() == 1 and view.page == 0)
	var event_item := view.entries.get_root().get_first_child()
	event_item.select(0)
	view._show_selected()
	assert("Продолжительность: 4.00" in view.details.text)
	view.category.select(0)
	view.search.text = "засуха"
	view._apply_filter()
	assert(view.matches.size() == 1, "Search must use translated visible event names")
	view.search.text = "branch-0"
	view._apply_filter()
	assert(view.matches.size() == 1, "Events older than the former 24-entry cutoff must remain searchable")
	view.search.text = "нет такого события"
	view._apply_filter()
	assert(view.matches.is_empty() and view.next_button.disabled and view.previous_button.disabled)
	view.search.text = ""
	sim.timeline.append({"name":"Meteor impact","time":400.0})
	view.refresh_requested.emit()
	assert(view.matches.size() == 252)
	sim.timeline.clear()
	view.refresh_requested.emit()
	assert(view.history.is_empty() and view.matches.is_empty())
	window.queue_free()
	await process_frame
	print("Evolution history tests passed: full history, bounded pagination, translated search, category filtering, event details, refresh and empty state")
	quit(0)
