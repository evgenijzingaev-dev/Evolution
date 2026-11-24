// Локальные типы Codebuff/Freebuff для .agents/*.ts.
// Основаны на официальном AgentDefinition (codebuff.com/docs/agents/agent-reference).
// Файл нужен только для типизации и автодополнения; во время выполнения
// type-only импорт удаляется транспилятором.

export type ToolName =
  | 'add_subgoal'
  | 'browser_logs'
  | 'code_search'
  | 'create_plan'
  | 'end_turn'
  | 'find_files'
  | 'glob'
  | 'list_directory'
  | 'read_docs'
  | 'read_files'
  | 'run_file_change_hooks'
  | 'run_terminal_command'
  | 'set_output'
  | 'skill'
  | 'spawn_agents'
  | 'str_replace'
  | 'think_deeply'
  | 'update_subgoal'
  | 'web_search'
  | 'write_file'

export type Prompt = string | { path: string }

export type OutputMode = 'last_message' | 'all_messages' | 'structured_output'

export interface JsonObjectSchema {
  type: 'object'
  properties?: Record<string, unknown>
  required?: string[]
  [key: string]: unknown
}

export interface InputSchema {
  prompt?: {
    type: 'string'
    description?: string
  }
  params?: {
    type: 'object'
    properties?: Record<string, unknown>
    required?: string[]
    [key: string]: unknown
  }
  [key: string]: unknown
}

export interface AgentDefinition {
  id: string
  version?: string
  publisher?: string
  displayName: string
  spawnerPrompt?: string
  model: string
  reasoningOptions?: {
    enabled?: boolean
    exclude?: boolean
    effort?: 'low' | 'medium' | 'high'
  }
  outputMode?: OutputMode
  outputSchema?: JsonObjectSchema
  includeMessageHistory?: boolean
  inheritParentSystemPrompt?: boolean
  toolNames?: ToolName[]
  spawnableAgents?: string[]
  inputSchema?: InputSchema
  systemPrompt?: Prompt
  instructionsPrompt?: Prompt
  stepPrompt?: Prompt
  handleSteps?: (context: {
    agentState: unknown
    prompt: string
    params: Record<string, unknown>
  }) => Generator<unknown, void, unknown>
}
