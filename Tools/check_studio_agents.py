#!/usr/bin/env python3
"""Проверяет постоянную команду субагентов в .agents/ без исполнения TS.

Определения загружаются Codebuff при старте сессии и выполняют авторский код,
поэтому здесь проверяется только структура: обязательные поля, уникальность id,
известные инструменты и ссылки spawnableAgents на существующие локальные роли.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AGENTS_DIR = ROOT / ".agents"

REQUIRED_FIELDS = ("displayName", "model", "spawnerPrompt", "instructionsPrompt")
ALLOWED_TOOLS = {
    "add_subgoal",
    "browser_logs",
    "code_search",
    "create_plan",
    "end_turn",
    "find_files",
    "glob",
    "list_directory",
    "read_docs",
    "read_files",
    "run_file_change_hooks",
    "run_terminal_command",
    "set_output",
    "skill",
    "spawn_agents",
    "str_replace",
    "think_deeply",
    "update_subgoal",
    "web_search",
    "write_file",
}

ID_RE = re.compile(r"\bid\s*:\s*['\"]([a-z0-9-]+)['\"]")
TOOLS_RE = re.compile(r"\btoolNames\s*:\s*\[(.*?)\]", re.S)
SPAWNS_RE = re.compile(r"\bspawnableAgents\s*:\s*\[(.*?)\]", re.S)
QUOTED_RE = re.compile(r"['\"]([^'\"]+)['\"]")


def check_file(path: Path) -> tuple[str | None, list[str]]:
    text = path.read_text(encoding="utf-8")
    errors: list[str] = []
    match = ID_RE.search(text)
    if not match:
        return None, [f"{path.name}: не найден id"]
    agent_id = match.group(1)
    for field in REQUIRED_FIELDS:
        if not re.search(rf"\b{field}\s*:", text):
            errors.append(f"{path.name}: отсутствует {field}")
    if "export default definition" not in text:
        errors.append(f"{path.name}: нет export default definition")

    tools_match = TOOLS_RE.search(text)
    tools = QUOTED_RE.findall(tools_match.group(1)) if tools_match else []
    if not tools:
        errors.append(f"{path.name}: пустой или отсутствующий toolNames")
    for tool in tools:
        if tool not in ALLOWED_TOOLS:
            errors.append(f"{path.name}: неизвестный инструмент {tool}")

    return agent_id, errors


def main() -> int:
    if not AGENTS_DIR.is_dir():
        print("ОШИБКА: каталог .agents/ отсутствует", file=sys.stderr)
        return 1

    files = sorted(
        p
        for p in AGENTS_DIR.glob("*.ts")
        if not p.name.endswith(".d.ts")
    )
    if not files:
        print("ОШИБКА: в .agents/ нет определений *.ts", file=sys.stderr)
        return 1

    errors: list[str] = []
    ids: dict[str, Path] = {}
    spawns: dict[str, list[str]] = {}
    tools_by_id: dict[str, list[str]] = {}

    for path in files:
        agent_id, file_errors = check_file(path)
        errors.extend(file_errors)
        if not agent_id:
            continue
        if agent_id in ids:
            errors.append(f"{path.name}: повторный id {agent_id} (уже в {ids[agent_id].name})")
            continue
        ids[agent_id] = path
        text = path.read_text(encoding="utf-8")
        spawn_match = SPAWNS_RE.search(text)
        spawns[agent_id] = QUOTED_RE.findall(spawn_match.group(1)) if spawn_match else []
        tools_match = TOOLS_RE.search(text)
        tools_by_id[agent_id] = QUOTED_RE.findall(tools_match.group(1)) if tools_match else []

    for agent_id, refs in spawns.items():
        for ref in refs:
            if "/" in ref:
                continue  # опубликованный агент вида publisher/name@version
            if ref not in ids:
                errors.append(f"{agent_id}: spawnableAgents ссылается на неизвестного {ref}")

    lead = "studio-lead"
    if lead not in ids:
        errors.append("нет обязательного агента studio-lead")
    elif "spawn_agents" not in tools_by_id.get(lead, []):
        errors.append("studio-lead должен иметь инструмент spawn_agents")

    if errors:
        print("ОШИБКИ определений субагентов:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Проверено агентов: {len(ids)}; ошибок нет.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
