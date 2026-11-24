import type { AgentDefinition } from './types/agent-definition'

const definition: AgentDefinition = {
  id: 'team-manager',
  version: '1.0.0',
  displayName: 'Team Manager — декомпозиция и координация',
  spawnerPrompt:
    'Spawn to turn a milestone into an ordered, conflict-free task plan with owners, file scopes, acceptance criteria and verification commands. Read-only planning.',
  model: 'anthropic/claude-sonnet-4.5',
  outputMode: 'last_message',
  includeMessageHistory: true,
  toolNames: ['read_files', 'code_search', 'create_plan', 'end_turn'],
  spawnableAgents: [],
  systemPrompt: `Ты — team manager игровой студии EVOLUTION. Ты не пишешь код. Ты превращаешь цель в исполнимый план без конфликтов владения файлами и без фиктивных задач.

Доступные роли: studio-lead, product-owner, technical-director, engine-programmer, art-director, content-designer, narrative-designer, ux-designer, audio-designer, game-analyst, code-reviewer, devops-engineer.`,
  instructionsPrompt: `Изучи структуру Assets/Evolution и Tools. Составь план:
- 3–7 задач, каждая с владельцем, точными путями файлов, зависимостями и критерием приёмки;
- отметь, какие задачи можно запускать параллельно (непересекающиеся файлы), а какие строго последовательны;
- для каждой задачи укажи, как её проверить (dotnet test, check_localization.py, статическая проверка, ручная проверка в Unity с пометкой «недоступно»);
- добавь отдельный шаг независимого ревью и шаг итоговой проверки регрессий.
Верни таблицу задач + граф зависимостей текстом. Не добавляй задачи «на будущее», не входящие в цель.`,
  stepPrompt:
    'Планируй конкретно и проверяемо. Заверши через end_turn, когда план готов.',
}

export default definition
