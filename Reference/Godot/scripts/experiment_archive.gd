extends RefCounted

const DIRECTORY := "user://experiments"

static func save_snapshot(sim: SimulationManager, label: String = "", directory: String = DIRECTORY, latest_path: String = SaveManager.SAVE_PATH) -> Dictionary:
	if sim._step_active: return {"error":ERR_BUSY,"path":""}
	var error := DirAccess.make_dir_recursive_absolute(directory)
	if error != OK: return {"error":error,"path":""}
	var filename := "%d_%d.json" % [int(Time.get_unix_time_from_system()), Time.get_ticks_usec()]
	var path := directory.path_join(filename)
	error = SaveManager.save_simulation(sim, path)
	if error != OK: return {"error":error,"path":""}
	var metadata := {"label":label.strip_edges(),"date":Time.get_datetime_string_from_system(),"planet":str(sim.planet_data().name),"population":sim.organisms.size(),"elapsed":sim.elapsed}
	var file := FileAccess.open(path + ".meta.json", FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(metadata))
		file.close()
	if not latest_path.is_empty():
		error = DirAccess.copy_absolute(path, latest_path + ".tmp")
		if error == OK: error = DirAccess.rename_absolute(latest_path + ".tmp", latest_path)
	return {"error":error,"path":path}

static func entries(directory: String = DIRECTORY, include_quick_slots: bool = true) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if include_quick_slots:
		for slot in [[SaveManager.SAVE_PATH,"Последнее ручное сохранение"],[SaveManager.AUTOSAVE_PATH,"Автосохранение"]]:
			if FileAccess.file_exists(slot[0]): result.append({"path":slot[0],"label":slot[1],"date":"","planet":"","population":-1,"elapsed":0.0})
	if not DirAccess.dir_exists_absolute(directory): return result
	var files := DirAccess.get_files_at(directory)
	files.sort()
	files.reverse()
	for filename in files:
		if not filename.ends_with(".json") or filename.ends_with(".meta.json"): continue
		var path := directory.path_join(filename)
		var entry := {"path":path,"label":"Эксперимент " + filename.get_basename(),"date":"","planet":"","population":-1,"elapsed":0.0}
		var sidecar := path + ".meta.json"
		if FileAccess.file_exists(sidecar):
			var file := FileAccess.open(sidecar, FileAccess.READ)
			if file != null and file.get_length() <= 16384:
				var metadata = JSON.parse_string(file.get_as_text())
				if metadata is Dictionary:
					for key in ["label", "date", "planet"]:
						if metadata.get(key) is String and not str(metadata[key]).is_empty(): entry[key] = str(metadata[key]).left(200)
					for key in ["population", "elapsed"]:
						if (metadata.get(key) is int or metadata.get(key) is float) and is_finite(float(metadata[key])): entry[key] = metadata[key]
		result.append(entry)
	return result
