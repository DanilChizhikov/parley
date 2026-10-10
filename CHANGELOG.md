# Changelog

## [0.1.0] - 2026-10-10

First release.

### Added
- `Window/DTech/Parley`: an AI chat window with streaming markdown (code highlighting, tables, task lists, clickable
  `path:line` references), collapsible thinking, tool cards with unified diffs, subagent output nested under its
  Agent card, cost and context usage, interrupt, and a message queue while the agent works. Follows the editor's
  light or dark skin.
- A side panel with the latest plan, todos, background tasks (with a Stop button), and the chat's MCP servers and
  skills.
- Claude Code backend: runs the `claude` CLI over the Agent SDK stream-json control protocol, with permission
  requests, `AskUserQuestion`, `ExitPlanMode` plan review, and permission mode, model and effort switching. Every
  sign-in method: CLI default, Claude subscription, Console, SSO, long-lived OAuth token (`claude setup-token`), API
  key, LLM gateway (with Ollama and LM Studio presets), `apiKeyHelper`, Amazon Bedrock, Google Vertex AI, Microsoft
  Foundry and Anthropic profiles, plus a per-profile `CLAUDE_CONFIG_DIR`.
- Codex backend: runs `codex app-server`, signed in through the CLI (ChatGPT login, device code) or with an OpenAI API
  key. Each profile gets its own Codex home under `Library/Parley/Codex`, or a `CODEX_HOME` you choose.
- Local-model backend: Parley's own agent loop over any OpenAI-compatible `/v1/chat/completions` server (LM Studio,
  Ollama, llama.cpp, vLLM, custom), with Read, Write, Edit, Glob, Grep, Bash, WebFetch, TodoWrite, AskUserQuestion,
  EnterPlanMode, ExitPlanMode and Skill tools, native or `<tool_call>` text tool calls, vision, a context budget that
  trims old output, `/compact` and `/clear`.
- Permission modes: Ask before edits, Accept edits, Plan, Auto (Claude Code), Bypass permissions (asks to confirm,
  hidden in Claude Code's restricted mode) and Don't ask. Permission cards offer Allow once, Always allow (the rule is
  shown on the button), Deny and Deny with feedback. The plan card offers Approve · auto-accept edits, Approve · review
  each edit, Approve · bypass permissions and Keep planning.
- Unity editor tools for the agent: `console`, `compile_status`, `refresh`, `selection`, `hierarchy`, `inspect` and
  `project_info`. Served to Claude Code as an in-process MCP server, to Codex as dynamic tools and to local models
  natively; each can be switched off.
- Per-chat MCP servers: a library of your own stdio and HTTP servers (secrets in the OS keychain) plus the servers
  from the Claude Code or Codex config, each switched on or off per chat. Local models use Parley's own MCP client.
- Per-chat skills: a library of `SKILL.md` folders (create, import, edit) plus the skills the CLI finds, each switched
  on or off per chat, and auto-run skills with arguments that every new chat runs before its first message.
- Attachments: the selection, console errors, a Scene view or game camera screenshot, files, and drag & drop. `/`
  command autocomplete and `@` asset path completion.
- Sessions saved per project under `Library/Parley/Sessions`, resumed from History and reopened after a script reload
  or an editor restart.
- Script reloads are deferred while a turn runs, so recompiling doesn't stop the agent; the AssetDatabase is refreshed
  after a turn that edited files.
- Secrets kept in the macOS Keychain, the Windows Credential Manager or Secret Service, with a `0600` file fallback.
- Shared project options in `ProjectSettings/ParleySettings.asset`, ignored after an outside change until you trust
  them.
- Toolbar update notifier: a green "Update Available" dropdown with "What changed" (release notes for every newer
  version) and "Update" (updates the package through Package Manager and recompiles scripts).
