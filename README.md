# Parley
[![Unity Version](https://img.shields.io/badge/unity-6000.0+-000.svg)](https://unity.com/releases/editor/archive)
![Unity Tests](https://github.com/DanilChizhikov/parley/actions/workflows/tests.yml/badge.svg?branch=master)

## Overview
Parley is an AI chat window for the Unity Editor. It works with two kinds of backends:

- **Claude Code.** Parley runs the `claude` CLI as a child process and talks to it over the same stream-json control
  protocol the Claude Agent SDK uses. Every sign-in method Claude Code supports is available: Claude subscription,
  Console, SSO, long-lived OAuth token, API key, LLM gateway, `apiKeyHelper`, Amazon Bedrock, Google Vertex AI,
  Microsoft Foundry and Anthropic profiles.
- **Local models.** Parley runs its own agent loop against any OpenAI-compatible server: LM Studio, Ollama,
  llama.cpp, vLLM, or a hosted endpoint. The agent has file, search, shell and web tools, asks before risky actions,
  and supports plan mode and clarifying questions.

Both backends share one interface. You get streaming markdown, collapsible thinking, tool cards with diffs, permission
prompts, `AskUserQuestion` cards, plan review, permission modes, todos, background tasks and subagents, interrupt,
resumable sessions, and cost and context usage. Parley also exposes **Unity editor tools** to the agent: console,
compiler errors, refresh, selection, hierarchy, inspector values and project info. You can attach the selection,
console errors, a Scene or Game screenshot, or files to a message.

## License
MIT. See [LICENSE](LICENSE).