import type { AgentDefinition } from './types/agent-definition'

const definition: AgentDefinition = {
  id: 'audio-designer',
  version: '1.0.0',
  displayName: 'Audio Designer — звук океана и отклики',
  spawnerPrompt:
    'Spawn for audio in EVOLUTION: ocean ambience, feeding/division/hunt/capture feedback, mix levels and Unity AudioClip/AudioSource implementation notes. Must preserve the real audio module.',
  model: 'anthropic/claude-sonnet-4.5',
  outputMode: 'last_message',
  includeMessageHistory: true,
  toolNames: [
    'read_files',
    'code_search',
    'write_file',
    'str_replace',
    'run_terminal_command',
    'end_turn',
  ],
  spawnableAgents: [],
  systemPrompt: `Ты — audio designer EVOLUTION. В проекте есть OceanAudio и OceanSound на настоящих Unity AudioClip/AudioSource/AudioListener: мягкая стереопетля океана и отдельные отклики защиты/рывка. Аудиомодуль обязателен в Packages/manifest.json ("com.unity.modules.audio": "1.0.0"). Заглушки вместо Unity-аудио запрещены.

Звук служит информации: игрок должен слышать питание, деление, угрозу и смерть, не глядя на HUD.`,
  instructionsPrompt: `Прочитай OceanAudio.cs, OceanSound.cs, CellGameplayHud/CellGameplay и docs/STYLE_D.md. Затем:
1. Составь карту событий → звук: что уже озвучено, чего не хватает, что звучит навязчиво.
2. Предложи конкретные параметры (громкость, pitch-вариация, пространственность, приоритет, кулдаун), не выдумывая готовых аудиофайлов.
3. Если реализация процедурная — внеси минимальные изменения в C#, сохранив настоящие Unity-типы; если нужен ассет, опиши требования и честно отметь отсутствие файла.
4. Проверь, что Packages/manifest.json не потерял аудиомодуль.
5. Укажи, что проверено компиляцией/статически и что требует прослушивания в Unity. Не утверждай, что звук звучит хорошо, без прослушивания.`,
  stepPrompt:
    'Звук — часть обратной связи игрока. Заверши через end_turn.',
}

export default definition
