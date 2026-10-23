# Parley
[![Unity Version](https://img.shields.io/badge/unity-6000.0+-000.svg)](https://unity.com/releases/editor/archive)
![Unity Tests](https://github.com/DanilChizhikov/parley/actions/workflows/tests.yml/badge.svg?branch=master)
[![Donate](https://img.shields.io/badge/donate-DonationAlerts-f59c07.svg)](https://www.donationalerts.com/r/danilchizhikov)

## Overview
Parley is an AI chat window for the Unity Editor. It works with three kinds of backends:

- **Claude Code.** Parley runs the `claude` CLI as a child process and talks to it over the same stream-json control
  protocol the Claude Agent SDK uses. Every sign-in method Claude Code supports is available: Claude subscription,
  Console, SSO, long-lived OAuth token, API key, LLM gateway, `apiKeyHelper`, Amazon Bedrock, Google Vertex AI,
  Microsoft Foundry and Anthropic profiles.
- **Codex.** Parley runs `codex app-server` and drives it over its JSON-RPC protocol, signed in with ChatGPT or an
  OpenAI API key.
- **Local models.** Parley runs its own agent loop against any OpenAI-compatible server: LM Studio, Ollama,
  llama.cpp, vLLM, or a hosted endpoint. The agent has file, search, shell and web tools, asks before risky actions,
  and supports plan mode and clarifying questions.

All backends share one interface. You get streaming markdown, collapsible thinking, tool cards with diffs, permission
prompts, `AskUserQuestion` cards, plan review, permission modes, todos, background tasks and subagents, interrupt,
resumable sessions, and cost and context usage. Each chat picks its own MCP servers and skills. Parley also exposes
**Unity editor tools** to the agent: console, compiler errors, refresh, selection, hierarchy, inspector values and
project info. You can attach the selection, console errors, a Scene or Game screenshot, or files to a message.

![The Parley window with the side panel open on the MCP tab, listing servers from the Claude Code config that can be switched on for this chat](Documentation~/images/mcp-panel.png)

## Table of Contents
- [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [UPM Installation](#upm-installation)
- [Features](#features)
- [Usage](#usage)
    - [Open the Window](#open-the-window)
    - [Profiles and Sign-in](#profiles-and-sign-in)
    - [Codex](#codex)
    - [Local Models](#local-models)
    - [Permission Modes](#permission-modes)
    - [Questions and Plans](#questions-and-plans)
    - [Unity Tools](#unity-tools)
    - [Panels](#panels)
    - [MCP Servers](#mcp-servers)
    - [Skills](#skills)
    - [Presets](#presets)
    - [Commands](#commands)
    - [Attachments and Commands](#attachments-and-commands)
    - [Sessions](#sessions)
    - [Token Usage and Plan Limits](#token-usage-and-plan-limits)
    - [Debug Preview](#debug-preview)
- [Project Settings and Trust](#project-settings-and-trust)
- [Script Reloads](#script-reloads)
- [Token Usage Compared to VS Code](#token-usage-compared-to-vs-code)
- [Updates](#updates)
- [Where Parley Stores Data](#where-parley-stores-data)
- [Troubleshooting](#troubleshooting)
- [Support](#support)
- [License](#license)

## Getting Started

### Prerequisites
- [Unity](https://unity.com/releases/editor/archive) 6000.0+
- For Claude Code profiles: [Claude Code](https://code.claude.com/docs/en/setup) 2.1 or later
- For Codex profiles: the [Codex CLI](https://github.com/openai/codex) (`npm install -g @openai/codex` or
  `brew install codex`). API-key profiles need 0.162.0 or later
- For local profiles: a server with an OpenAI-compatible `/v1/chat/completions` endpoint that supports tool calling
  (for example LM Studio or Ollama with a model such as Qwen3-Coder)

### UPM Installation
1. Open the manifest.json file in your project's Packages folder.
2. Add the following line to the dependencies section:
    ```json
    "com.dtech.parley": "https://github.com/DanilChizhikov/parley.git",
    ```
3. Unity will automatically import the package.

To pin a release, add its tag: `https://github.com/DanilChizhikov/parley.git#v0.3.0`.

Parley depends on `com.unity.nuget.newtonsoft-json`; Unity installs it automatically.

## Features
- Claude Code backend over the Agent SDK control protocol: tool permission requests, `AskUserQuestion`,
  `ExitPlanMode` plan review, interrupts, permission mode and model switching, and context usage
- Every Claude Code sign-in method, with secrets kept in the OS keychain
- Codex backend over `codex app-server`: approvals, plan mode, reasoning effort, MCP servers and skills, with a
  separate Codex home per profile so your terminal login stays untouched
- A local-model agent with Read, Write, Edit, Glob, Grep, Bash, WebFetch, TodoWrite, AskUserQuestion, EnterPlanMode,
  ExitPlanMode and Skill tools
- Native function calling, or `<tool_call>` text tags for models that lack it
- Streaming markdown with code highlighting, tables, task lists and clickable `path:line` references
- Tool cards with unified diffs for edits and collapsible output
- Subagent output nested under its Agent tool card
- Side panel with the latest plan, todos, background tasks (with a Stop button), this chat's MCP servers and
  skills (with search), and its token usage
- Token usage per model, agent, turn and tool result, with an XML export to hand to an agent for analysis
- Claude Code and Codex plan limits next to Send: what is left of the 5-hour session and the weekly limit, and when
  they reset
- Unity editor tools served to Claude Code as an in-process MCP server (`mcp__unity__*`), to Codex as dynamic tools,
  and to local models natively (`unity_*`)
- Panels: editor windows the agent builds from a JSON spec, with no C# and no recompile
- Per-chat MCP servers: your own library (stdio or HTTP) plus the servers from the Claude Code or Codex config
- Per-chat skills: your own `SKILL.md` library plus the skills the CLI finds, and skills that run automatically
  before the first message of every new chat
- Attachments: selection, console errors, Scene view or game camera screenshots, files, and drag & drop
- `/` command autocomplete and `@` asset path completion
- Sessions saved per project, which you can resume after a script reload or an editor restart
- Script reloads are deferred while the agent works, so recompiling doesn't kill the turn
- An update notifier in the toolbar with release notes and one-click update

## Usage

### Open the Window
Use `Window/DTech/Parley/AI Chat`. The toolbar holds the profile, model, permission mode and effort selectors, the **MCP**,
**Skills** and **Preset** buttons, plus **New**, **History**, **Panel** and preferences. Press Enter to send and Shift+Enter for a
new line. While the agent works, Send queues your message and **Stop** interrupts the turn.

**Panel** toggles the side panel. Its tabs are **Todos**, **Plan**, **Tasks**, **MCP**, **Skills** and **Usage**.

### Profiles and Sign-in
Profiles live in `Preferences/DTech/Parley`. Parley starts with a Claude Code profile, a Codex profile and two local
ones (LM Studio and Ollama). Add, duplicate or delete profiles there. Changes apply to chats you start afterwards.

| Sign-in method | What Parley does |
|---|---|
| Claude Code default | Uses whatever the CLI is already signed in with |
| Claude subscription / Console / SSO | Runs `claude auth login --claudeai / --console / --sso` and shows the sign-in link and a field for the code. **Sign in (Terminal)** runs the same command in a terminal window |
| Long-lived OAuth token | **Generate token…** runs `claude setup-token` and saves the printed token. You can also paste a token. Passed as `CLAUDE_CODE_OAUTH_TOKEN` |
| API key | `ANTHROPIC_API_KEY` |
| LLM gateway / custom endpoint | `ANTHROPIC_BASE_URL` + `ANTHROPIC_AUTH_TOKEN` (+ custom headers). Presets point Claude Code at Ollama (0.14+) or LM Studio (0.4.1+) through their Anthropic-compatible APIs |
| apiKeyHelper | Passed through `--settings` |
| Amazon Bedrock | `CLAUDE_CODE_USE_BEDROCK`, region, and an AWS profile, access keys or a Bedrock API key |
| Google Vertex AI | `CLAUDE_CODE_USE_VERTEX`, `CLOUD_ML_REGION`, `ANTHROPIC_VERTEX_PROJECT_ID`, optional credentials file |
| Microsoft Foundry | `CLAUDE_CODE_USE_FOUNDRY`, resource or base URL, API key or Entra ID |
| Anthropic profile | `ANTHROPIC_PROFILE` |

Any Claude profile can set a **Config dir** (`CLAUDE_CONFIG_DIR`). This keeps separate accounts apart, and logins for
that profile are stored there. For every method except *Claude Code default*, Parley removes inherited credential
variables that the profile doesn't set. Without that, Claude Code's credential precedence could pick a different
login than the one you chose. **Check status** runs `claude auth status`.

Secrets go to the macOS Keychain, the Windows Credential Manager, or Secret Service (`secret-tool`) on Linux. If
`secret-tool` is missing, they go to a `0600` file in `~/.config/dtech-parley`. They are never written to project or
settings assets, and reach the CLI only through environment variables.

### Codex
A Codex profile signs in one of two ways:

| Sign-in method | What Parley does |
|---|---|
| Codex default | Uses the ChatGPT sign-in or API key Codex already has. **Sign in (Terminal)** runs `codex login`, **Sign in with device code (Terminal)** runs `codex login --device-auth`, and **Sign out (Terminal)** runs `codex logout` |
| OpenAI API key | The key is kept in the OS keychain and handed to Codex with credentials that are never written to disk |

Codex runs with its own home folder (`Library/Parley/Codex/<profile id>`) unless you set **Config dir**
(`CODEX_HOME`), so your terminal login stays untouched. **Check status** runs `codex login status`. **Extra CLI
arguments** are appended to `codex app-server`.

### Local Models
Pick a server preset (LM Studio, Ollama, llama.cpp, vLLM, or a custom OpenAI-compatible server) or enter a base URL
that ends in `/v1`. Then use **Test connection** and **Pick…** to choose a model. Set the context window to match the
server's setting; Parley trims old tool output, then old turns, to stay inside it. `/compact` summarizes the
conversation and `/clear` forgets it. Turn on **Vision** for models that accept images. Turn on **Text tool calls**
when the server or model has no native function calling.

The local agent reads project instruction files such as `CLAUDE.md`, `AGENTS.md` and `CODEX.md` from the project root
(you can switch this off) and the project's extra instructions.

### Permission Modes
| Mode | Behaviour |
|---|---|
| Ask before edits | Reads run freely; edits, shell commands and network calls ask first |
| Accept edits | File edits inside the project run without asking |
| Plan | Read-only exploration; the agent finishes with a plan for you to review |
| Auto (Claude Code) | Claude Code's classifier decides |
| Bypass permissions | Everything runs without asking. Parley asks you to confirm before switching |
| Don't ask | Anything that would ask is denied |

For Codex, each mode maps to an approval policy and a sandbox: *Ask before edits* and *Plan* run read-only, *Accept
edits* allows writes inside the workspace, and *Bypass permissions* gives full access. Codex has no *Auto* mode.

A permission card offers **Allow once**, **Always allow**, **Deny**, or **Deny with feedback**. For Claude Code,
**Always allow** applies Claude's suggested rule. For local models, it remembers the rule for this project: a command
prefix, file edits, or a host. The rule is shown on the button.

### Questions and Plans
When the agent calls `AskUserQuestion`, a card shows each question with its options. Questions can be single or
multiple choice, include an *Other* field, and show option previews when the model provides them. You can also reply
in your own words instead. In plan mode the agent presents its plan with `ExitPlanMode`. You can **Approve ·
auto-accept edits**, **Approve · review each edit**, **Approve · bypass permissions**, or **Keep planning** with
feedback. The latest plan stays in the side panel.

When Claude Code runs in restricted mode (`CLAUDE_CODE_RESTRICTED=1` or `--restricted`), Parley hides the *Bypass
permissions* mode and the matching plan button.

### Unity Tools
| Tool | Purpose |
|---|---|
| `console` | Console errors, warnings and logs, with an optional filter and stack traces |
| `compile_status` | Whether scripts are compiling, plus current compiler errors |
| `refresh` | Refreshes the AssetDatabase, waits for compilation, reports errors |
| `selection` | Selected assets and scene objects |
| `hierarchy` | Scene or prefab hierarchy with components |
| `inspect` | Serialized properties of an object, asset or the selection (read-only) |
| `project_info` | Unity version, build target, scripting backend, render pipeline, build scenes, packages |
| `panel_open` | Opens an editor window built from a JSON spec, or a saved panel (see [Panels](#panels)) |
| `panel_save` | Saves a panel spec under a name (asks first, like a file edit) |
| `panel_list` | Saved panels with their titles and targets |

The agent may use the read-only Unity tools without asking. You can switch each tool off in the preferences.

### Panels
Ask the agent for a window, for example *"make me a window to tune HP and speed of all EnemyConfig assets"*. The agent
describes it as a JSON spec and `panel_open` shows it as a regular editor window right away. No C# is written, no
scripts recompile, and nothing is added to `Assets`.

```json
{
  "title": "Enemy balance",
  "targets": { "boss": "Assets/Configs/Boss.asset" },
  "items": [
    { "type": "header", "text": "Boss" },
    { "type": "field", "target": "boss", "path": "_health", "label": "HP" },
    { "type": "table", "query": "t:EnemyConfig", "columns": ["_health", "_speed"] },
    { "type": "button", "label": "Save", "action": "save_assets" }
  ]
}
```

- Items: `header`, `label`, `help`, `separator`, `field` (one serialized property), `inspector` (a whole object),
  `group` / `row` (layout), `table` (one row per asset of an AssetDatabase query) and `button`.
- Fields are bound `SerializedProperty` fields: Undo, prefab overrides and your project's property drawers work.
- Targets are asset paths, GUIDs, instance ids or hierarchy paths (`Scene:Root/Child`). A missing scene object shows a
  placeholder with a Retry button.
- Buttons run only built-in actions: `ping`, `select`, `open`, `save_assets`, `menu` and `create_asset`. `menu` and
  `create_asset` ask you to confirm. Panels never run code from the spec.
- `panel_open` checks targets and property paths before it opens anything and sends errors back to the agent, with
  suggestions such as *"Did you mean: _health?"*.
- `repeat` renders its child items once per asset of a query, each in a foldout: inside it leave `target` out to mean
  the current asset. Use `table` for a compact overview and `repeat` when every asset needs real fields; both keep up
  on their own when assets are added, renamed or removed.
- Panels heal themselves. Parley stores a stable id per target next to the readable path and repairs the path after a
  rename or a move, so a renamed asset never breaks a panel. A target that cannot be reached right now (deleted asset,
  closed scene) says it will reconnect and comes back by itself.
- Open panels re-resolve their targets and re-run their queries when the project or the hierarchy changes, and keep
  your scroll position and foldout state. Unrelated imports cost nothing: a panel only rebuilds when something it
  shows actually changed.
- A panel built from explicit targets shows a banner when assets of the same type are not in it, with a Ping button.
- Save a panel from its toolbar or with `panel_save`. Saved panels live in `UserSettings/Parley/Panels/<name>.json` and
  reopen from `Window/DTech/Parley/Panels`. Open panels survive script reloads, and a panel saved again refreshes its
  window.

### MCP Servers
The **MCP** tab of the side panel lists the MCP servers of the current chat. Each one has a checkbox that switches it
on or off for this chat only, and shows whether it is connected, failed or needs a sign-in. The **All** checkbox in
each section header switches every server of that section at once.

- **Parley servers** are your own library. **+ Add** creates one: a name, a transport (stdio command with arguments
  and environment variables, or an HTTP URL with headers), and whether new chats use it by default. Secret values are
  kept in the OS keychain. The library is shared by all your projects.
- **From Claude Code config** / **From Codex config** lists the servers the CLI already knows about, so you can turn
  them off for a chat without editing the CLI config.

Local models talk to MCP servers through Parley's own MCP client. Every MCP tool asks before it runs there, whatever
the server claims about it. Some changes apply with your next message: the agent restarts and resumes the chat.

### Skills
The **Skills** tab lists the skills of the current chat, each with a checkbox for this chat. The **All** checkbox in
the header of **Parley skills** and **From Claude Code** / **From Codex** switches the whole section at once.

![The Skills tab: an auto-run skill with arguments, the empty Parley skill library, and skills from Claude Code](Documentation~/images/skills-panel.png)

- **Parley skills** are your own library of `SKILL.md` folders. **+ New** writes one (name, description, argument
  hint and instructions, where `$ARGUMENTS` is replaced with the arguments), **Import…** copies a folder that contains
  a `SKILL.md`, and **Folder** reveals the library.
- **From Claude Code** / **From Codex** lists the skills the CLI finds. Local models read `.claude/skills` and
  `.agents/skills` from the project and your home folder (**From skill folders**).
- **Auto-run in this chat** lists the skills, each with optional arguments, that this chat runs before your next
  message (for example `/caveman ultra`). **Add** queues one more, **Skip** leaves it out this time, **Remove** drops it
  from the chat.
- **Auto-run in new chats** is the list every new chat starts with when no default preset is set.

Claude Code runs each auto-run skill as a separate turn, so skill hooks see it. Codex receives skills as `skill`
input items. Local models get the skill text inline and can load more with their Skill tool.

### Presets
A preset stores the setup of a chat: which Parley and CLI skills and MCP servers are on, and the auto-run skills.
Set up a chat in the **MCP** and **Skills** tabs, then use **Preset ▾** in the toolbar:

- **Save chat as preset…** stores the current chat under a name. **Update from chat** overwrites an existing preset.
- Pick a preset at the top of the menu to apply it to the current chat. Its auto-run skills run with your next
  message. The toolbar shows the preset name while the chat still matches it.
- **Default for new chats** applies a preset to every new chat instead of the per-item defaults.
- **Rename** and **Delete** manage the list. Presets live in your user settings and are shared by all projects.

A preset turns off only the CLI skills and servers it lists as off, so skills installed after you saved it stay on
until you update the preset.

### Commands
Type `/` in the composer to see Parley's own commands next to the backend's commands and skills.

- `/create-parley-panel <what the panel should let you tune>` runs the panel workflow: the agent resolves the targets,
  reads their real property paths with `inspect`, opens the window with `panel_open` and stops there. You name the
  panel and press **Save** in its toolbar if you want to keep it.
- A command is a prompt: Parley swaps what you typed for the command's instructions before the message reaches the
  agent, so the chat still shows the line you wrote.

Add your own command from any editor assembly:

```csharp
using DTech.Parley.Editor.Commands;

public sealed class ReviewConfigsCommand : IParleyCommand
{
    public string Name => "review-configs";
    public string Description => "Review balance configs";
    public string ArgumentHint => "<folder>";

    public string BuildPrompt(ParleyCommandRequest request)
    {
        return "Review every ScriptableObject under " + request.Arguments + " and report risky values.";
    }
}
```

The type must be public with a public parameterless constructor, and the name uses lowercase letters, digits and `-`.
Commands are always available, there is nothing to switch on. A command of yours that takes the name of a built-in one
replaces it.

### Attachments and Commands
<img src="Documentation~/images/attach-menu.png" alt="The Attach menu: Selection, Console errors, Scene view screenshot, Game camera screenshot, File…" width="180">

**＋ Attach** adds the selection, console errors, a Scene view or game camera screenshot, or a file. You can also
drag assets, files or scene objects onto the composer. Type `/` for Parley's commands, the backend's commands and
skills, or `@` to complete an asset path.

### Sessions
Parley saves each chat under `Library/Parley/Sessions`, with its MCP servers and skills. **History** resumes a chat:
Claude Code continues with `--resume`, Codex resumes its thread, and local chats restore their message history.
Parley reopens the last chat after a script reload or an editor restart.

### Token Usage and Plan Limits
The **Usage** tab of the side panel shows what the chat has spent: totals per model and per agent (subagents
separately), every turn with its calls, the largest tool results, peak context and cost. When a pause outlives the
prompt cache and the whole context is written again, the tab says so. **Copy XML** and **Export XML…** save the report,
so you can paste it into a chat and ask the agent where the tokens went. Usage is recorded for Claude Code, Codex and
local models.

With a Claude Code or Codex subscription, a circle next to **Send** shows what is left of your plan: the 5-hour session
on the left half and the weekly limit on the right. Click it for the percentages, a countdown and the reset time.

### Debug Preview
The preview is for working on Parley itself. Add `PARLEY_DEBUGG` to **Project Settings > Player > Scripting Define
Symbols**, then `Window/DTech/Parley/AI Chat Debug Preview` opens a separate Parley window driven by a scripted agent.
It never starts a CLI or calls a model, and its chats are not saved, so you can look at every part of the UI without
spending tokens.

- **Preview ▾** in the toolbar plays a scenario: markdown, thinking, tool cards, diffs, a subagent, permission requests,
  questions, plan approval, todos and background tasks, notices, the sign-in card, a crash, a slow turn to try **Stop**,
  a full context window and the restart hint. The same scenarios are `/` commands in the composer (`/showcase` plays
  them all).
- The profile menu switches between Claude Code, Codex and local model layouts.
- **Preview ▾ > Windows** opens a panel with every item type, a panel with spec errors, an empty panel, the saved
  panels list, the release notes window and the preferences.
- The MCP and Skills tabs, the preferences and saved panels are the real ones: changes there are saved as usual.

## Project Settings and Trust
The **This project** section of the preferences holds options shared through version control in
`ProjectSettings/ParleySettings.asset`: extra instructions, extra instruction files, extra directories the agent may
access, and the Unity tools. When that file changes outside Parley, for example after a pull, Parley ignores the
values until you press **Trust**. Trusting applies to chats started afterwards.

## Script Reloads
When the agent edits C# files, Unity recompiles, and the assembly reload that follows would kill the CLI process and
stop the local agent. By default Parley holds `EditorApplication.LockReloadAssemblies` while a turn runs. Compilation
and the `refresh` tool's error report still work, and the reload happens as soon as the turn ends. After a turn that
edited files, Parley refreshes the AssetDatabase. You can turn the lock off in the preferences (**Defer script reload
while the agent works**).

## Token Usage Compared to VS Code
Parley and the Claude Code extension for VS Code run the same `claude` CLI, so the agent does the same work in both.
The difference is the context each one sends with every request. We ran one read-only prompt (glob, three file reads,
two greps, a fixed 20-line answer) twice in each client, in fresh chats, with the same model (Claude Opus 5.5), the
same effort and the same user-level settings, skills and hooks.

| Run      | Starting context | Requests | Peak context | Cache read | Cache write | Output | Tool result chars |
|----------|-----------------:|---------:|-------------:|-----------:|------------:|-------:|------------------:|
| VS Code  |            45.1k |        4 |        56.5k |       166k |       30.8k |   1860 |             23.1k |
| Parley   |            23.5k |        3 |        35.6k |        59k |       25.4k |   1557 |             22.9k |
| VS Code  |            43.4k |        5 |        56.6k |       212k |       30.9k |   2172 |             26.6k |
| Parley   |            23.7k |        3 |        35.7k |        68k |       19.0k |   1431 |             22.8k |

- **Starting context.** Parley starts about 20k tokens smaller (23.6k vs 44.3k on average). The VS Code extension adds
  its own system prompt sections, IDE MCP tools and editor context. The same gap shows in the first request of 40
  earlier sessions.
- **Agent work.** Tool output and model output were close in both clients. The extra one or two requests in VS Code
  are within normal run-to-run variation.
- **Cost.** Weighting cache reads at 0.1×, cache writes at 1.25× and output at 5× the input price, a run cost about
  42k input-token equivalents in Parley and about 68k in VS Code, roughly 38% less.
- **Long turns.** Every request re-reads the whole context, so the 20k gap repeats on each one. A turn with 80
  requests re-reads about 1.5M more tokens in VS Code.

This is a small sample: two runs per client, Claude Code 2.1.306 (VS Code) and 2.1.308 (Parley), measured on
2026-10-22. The starting context barely changes between runs, so that gap is reliable. The request count of a long
task varies by about ±40% between runs, and this test doesn't show whether turns run longer in either client. Your
numbers depend on your plugins, skills, MCP servers and `CLAUDE.md` files, which both clients load.

On a real task the gap is smaller. Fixing a bug with a skill command and a 50 KB attached file took 19 requests and
about 238k input-token equivalents in Parley, and 18 requests and about 244k in VS Code. Parley's smaller context saved
about 340k cache reads over the turn, and a colder prompt cache on the first request took most of that back.

## Updates
Once per editor session Parley checks for a newer release: a Git tag on GitHub, or a newer version in the registry
when the package was installed from one. When there is one, the toolbar shows a green **Update Available** dropdown:

- **What changed** shows the release notes of every version newer than yours.
- **Update** updates the package through the Package Manager. Unity then recompiles scripts, which interrupts running
  chats.

## Where Parley Stores Data
- `Preferences/.../DTech/Parley/Settings.asset`: profiles (no secrets), MCP server library, skill settings, window
  state, per-project allow rules
- `Preferences/.../DTech/Parley/Skills/.claude/skills`: your skill library
- `ProjectSettings/ParleySettings.asset`: shared project options (extra instructions, extra directories, Unity tools)
- `Library/Parley/Sessions`: saved chats
- `UserSettings/Parley/Panels`: saved panels
- `Library/Parley/Codex`: per-profile Codex homes
- `Library/Parley/run`: generated `--mcp-config` / `--settings` files for the CLI
- OS keychain (or `~/.config/dtech-parley/credentials.json`): secrets

## Troubleshooting
- **Claude Code or Codex CLI not found.** Unity started from the Dock doesn't see your shell's `PATH`. Parley searches
  the usual install locations and asks your login shell. You can also set the path in the preferences and use
  **Detect** to check it.
- **Sign in doesn't finish inside Unity.** Use **Sign in (Terminal)**, then **Check status**.
- **A Codex API-key profile fails to start.** Update Codex to 0.162.0 or later.
- **An MCP server needs a sign-in.** For Claude Code, run `/mcp` in Claude Code; for Codex, run `codex mcp login`.
- **A local model never calls tools.** Use a model with tool support, or turn on **Text tool calls**.
- **Hooks, MCP servers or `node` fail under Claude Code.** Parley passes your login shell's `PATH` to the CLI. Make
  sure those tools are on it.

## Support
Parley is free and MIT-licensed. If it saves you time and you would like to help it grow, you can support its
development on [DonationAlerts](https://www.donationalerts.com/r/danilchizhikov).

It is entirely optional: bug reports, ideas and pull requests help
just as much.

## License
MIT. See [LICENSE](LICENSE).
