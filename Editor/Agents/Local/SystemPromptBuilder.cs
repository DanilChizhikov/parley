using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DTech.Parley.Editor.Agents.Local
{
	internal static class SystemPromptBuilder
	{
		private const int MaxInstructionChars = 20000;

		private static readonly string[] DefaultInstructionFiles =
		{
			"CLAUDE.md",
			"AGENTS.md",
			"CODEX.md",
			".claude/CLAUDE.md",
			".claude/AGENTS.md",
			".agents/AGENTS.md",
			".codex/AGENTS.md",
			".codex/CODEX.md",
		};

		public static string Build(SystemPromptRequest request)
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendLine("You are Parley, a coding agent running inside the Unity Editor. You help the user with their Unity project by reading and changing files, running commands and using Unity editor tools.");
			builder.AppendLine();
			builder.AppendLine("# Environment");
			builder.Append("- Project root: ").AppendLine(ProjectPaths.Root);
			builder.Append("- Unity ").Append(request.UnityVersion).Append(" on ").AppendLine(SystemInfo.operatingSystem);
			builder.Append("- Date: ").AppendLine(DateTime.Now.ToString("yyyy-MM-dd"));
			builder.Append("- Permission mode: ").AppendLine(PermissionModes.DisplayName(request.Mode));
			builder.AppendLine();
			builder.AppendLine("# How to work");
			builder.AppendLine("- Inspect the project with tools before answering questions about it. Paths may be absolute or relative to the project root.");
			builder.AppendLine("- Read a file before editing it. Prefer Edit for changes and Write for new files. Keep changes minimal and match the surrounding code style.");
			builder.AppendLine("- Never edit files under Library/ or Temp/ and never change GUIDs in .meta files by hand.");
			if (request.Catalog.TryGet(ToolCatalog.UnityLocalPrefix + "refresh", out _))
			{
				builder.AppendLine("- After changing C# scripts call unity_refresh and fix the compiler errors it reports. Use unity_console to inspect runtime errors and unity_hierarchy / unity_inspect for scenes.");
			}

			builder.AppendLine("- Track multi-step work with TodoWrite. Use AskUserQuestion only when a decision is genuinely the user's.");
			builder.AppendLine("- When a tool call is denied, do not retry it unchanged; adjust or ask.");
			builder.AppendLine("- Answer concisely in markdown. Reference code as path:line.");
			if (request.Mode == PermissionMode.Plan)
			{
				builder.AppendLine();
				builder.AppendLine("# Plan mode is active");
				builder.AppendLine("Do not modify files or run commands that change anything. Explore with read-only tools (Read, Glob, Grep, unity_*), ask clarifying questions with AskUserQuestion, then call ExitPlanMode with a complete markdown plan: context, the steps, the files to change and how to verify. Do not ask for approval in plain text — ExitPlanMode does that.");
			}

			if (request.TextToolCalls)
			{
				AppendTextToolProtocol(builder, request.Catalog);
			}

			AppendProjectInstructions(builder, request.Settings);
			if (!string.IsNullOrWhiteSpace(request.Settings.AppendSystemPrompt))
			{
				builder.AppendLine();
				builder.AppendLine("# Additional instructions");
				builder.AppendLine(request.Settings.AppendSystemPrompt.Trim());
			}

			return builder.ToString().TrimEnd();
		}

		public static JArray FunctionTools(ToolCatalog catalog)
		{
			JArray tools = new JArray();
			foreach (IParleyTool tool in catalog.Tools)
			{
				tools.Add(new JObject
				{
					["type"] = "function",
					["function"] = new JObject
					{
						["name"] = tool.Name,
						["description"] = tool.Description,
						["parameters"] = tool.InputSchema,
					},
				});
			}

			return tools;
		}

		private static void AppendTextToolProtocol(StringBuilder builder, ToolCatalog catalog)
		{
			builder.AppendLine();
			builder.AppendLine("# Calling tools");
			builder.AppendLine("Call a tool by writing exactly one block per call:");
			builder.AppendLine("<tool_call>{\"name\": \"ToolName\", \"arguments\": {\"param\": \"value\"}}</tool_call>");
			builder.AppendLine("After your calls you receive <tool_response name=\"ToolName\">...</tool_response> blocks. Available tools:");
			foreach (IParleyTool tool in catalog.Tools)
			{
				builder.Append("- ").Append(tool.Name).Append(": ").Append(tool.Description).Append(" Parameters: ")
					.AppendLine(tool.InputSchema.ToString(Formatting.None));
			}
		}

		private static void AppendProjectInstructions(StringBuilder builder, ParleyProjectSettings settings)
		{
			if (!settings.IncludeProjectInstructions)
			{
				return;
			}

			HashSet<string> appended = new (CommandLine.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
			foreach (string name in DefaultInstructionFiles)
			{
				AppendInstructionFile(builder, name, appended);
			}

			foreach (string name in settings.InstructionFiles)
			{
				if (!string.IsNullOrWhiteSpace(name))
				{
					AppendInstructionFile(builder, name.Trim(), appended);
				}
			}
		}

		private static void AppendInstructionFile(StringBuilder builder, string name, HashSet<string> appended)
		{
			string path = ProjectPaths.Resolve(name);
			if (!File.Exists(path) || !appended.Add(ProjectPaths.Normalize(path)))
			{
				return;
			}

			string text = File.ReadAllText(path);
			if (text.Length > MaxInstructionChars)
			{
				text = text.Substring(0, MaxInstructionChars) + "\n... [truncated]";
			}

			builder.AppendLine();
			builder.Append("# Project instructions (").Append(name).AppendLine(")");
			builder.AppendLine(text.Trim());
		}
	}
}