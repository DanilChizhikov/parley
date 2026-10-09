using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
	internal sealed class PermissionGate
	{
		public const string FileEditRuleTool = "FileEdit";

		private static readonly string[] SafeCommands =
		{
			"ls", "pwd", "cat", "head", "tail", "wc", "grep", "rg", "which", "echo", "file", "stat", "du", "df", "tree",
			"git status", "git diff", "git log", "git show", "git branch", "git remote", "git rev-parse", "git blame",
		};

		private readonly string _projectRoot;
		private readonly Func<IReadOnlyList<string>> _extraDirectories;
		private readonly Func<IEnumerable<AllowRule>> _rules;

		public PermissionGate(string projectRoot, Func<IReadOnlyList<string>> extraDirectories, Func<IEnumerable<AllowRule>> rules)
		{
			_projectRoot = projectRoot;
			_extraDirectories = extraDirectories;
			_rules = rules;
		}

		public static bool IsSafeCommand(string command)
		{
			if (string.IsNullOrWhiteSpace(command) || command.IndexOfAny(new[] { ';', '&', '|', '>', '<', '`', '$', '\n' }) >= 0)
			{
				return false;
			}

			string trimmed = command.Trim();
			foreach (string safe in SafeCommands)
			{
				if (trimmed == safe || trimmed.StartsWith(safe + " ", StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}

		public static AllowRule SuggestRule(IParleyTool tool, JObject input, string projectRoot)
		{
			string pattern = "*";
			string ruleTool = tool.Name;
			switch (tool.Kind)
			{
				case ToolKind.FileEdit:
					ruleTool = FileEditRuleTool;
					break;
				case ToolKind.Execute:
					pattern = CommandPrefix(ToolInput.String(input, "command", string.Empty));
					break;
				case ToolKind.Network:
					pattern = Uri.TryCreate(ToolInput.String(input, "url", string.Empty), UriKind.Absolute, out Uri uri) ? uri.Host : "*";
					break;
			}

			return new AllowRule(projectRoot, ruleTool, pattern);
		}

		public static string CommandPrefix(string command)
		{
			string[] tokens = command.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 0)
			{
				return "*";
			}

			if (tokens.Length > 1 && !tokens[1].StartsWith("-") && !tokens[1].Contains("/") && !tokens[1].Contains("."))
			{
				return tokens[0] + " " + tokens[1];
			}

			return tokens[0];
		}

		public static string DescribeRule(AllowRule rule)
		{
			return rule.Tool + (rule.Pattern == "*" ? string.Empty : "(" + rule.Pattern + ")");
		}

		public GateResult Evaluate(IParleyTool tool, JObject input, PermissionMode mode)
		{
			string path = tool.TargetPath(input);
			bool outside = path != null && !IsInsideWorkspace(path);
			if (tool.Kind == ToolKind.Interactive)
			{
				return mode == PermissionMode.DontAsk && tool.Name != "TodoWrite"
					? new GateResult(GateVerdict.Deny, "Interactive tools are disabled in Don't ask mode.")
					: new GateResult(GateVerdict.Allow, null);
			}

			if (mode == PermissionMode.Plan && (tool.Kind == ToolKind.FileEdit || tool.Kind == ToolKind.Execute))
			{
				return new GateResult(GateVerdict.Deny,
					"Plan mode is active: files can't be changed and commands can't run yet. Finish exploring, then call ExitPlanMode with your plan.");
			}

			if (mode == PermissionMode.BypassPermissions)
			{
				return new GateResult(GateVerdict.Allow, null);
			}

			GateResult ask = mode == PermissionMode.DontAsk
				? new GateResult(GateVerdict.Deny, "Don't ask mode denies actions that need approval.")
				: new GateResult(GateVerdict.Ask, outside ? "Path is outside the project: " + path : null);

			switch (tool.Kind)
			{
				case ToolKind.ReadOnly:
					return outside ? ask : new GateResult(GateVerdict.Allow, null);
				case ToolKind.FileEdit:
					if (!outside && (mode == PermissionMode.AcceptEdits || HasRule(FileEditRuleTool, "*")))
					{
						return new GateResult(GateVerdict.Allow, null);
					}

					return ask;
				case ToolKind.Execute:
					string command = ToolInput.String(input, "command", string.Empty);
					return IsSafeCommand(command) || MatchesCommandRule(tool.Name, command) ? new GateResult(GateVerdict.Allow, null) : ask;
				case ToolKind.Network:
					Uri.TryCreate(ToolInput.String(input, "url", string.Empty), UriKind.Absolute, out Uri uri);
					return uri != null && HasRule(tool.Name, uri.Host) ? new GateResult(GateVerdict.Allow, null) : ask;
				default:
					return HasRule(tool.Name, "*") ? new GateResult(GateVerdict.Allow, null) : ask;
			}
		}

		private bool IsInsideWorkspace(string path)
		{
			if (ProjectPaths.IsInside(path, _projectRoot))
			{
				return true;
			}

			foreach (string directory in _extraDirectories())
			{
				if (!string.IsNullOrWhiteSpace(directory) && ProjectPaths.IsInside(path, ProjectPaths.Resolve(directory)))
				{
					return true;
				}
			}

			return false;
		}

		private bool HasRule(string tool, string pattern)
		{
			foreach (AllowRule rule in _rules())
			{
				if (rule.Tool == tool && (rule.Pattern == "*" || rule.Pattern == pattern))
				{
					return true;
				}
			}

			return false;
		}

		private bool MatchesCommandRule(string tool, string command)
		{
			if (command.IndexOfAny(new[] { ';', '&', '|', '`', '$', '\n' }) >= 0)
			{
				return false;
			}

			string trimmed = command.Trim();
			foreach (AllowRule rule in _rules())
			{
				if (rule.Tool == tool && (trimmed == rule.Pattern || trimmed.StartsWith(rule.Pattern + " ", StringComparison.Ordinal)))
				{
					return true;
				}
			}

			return false;
		}
	}
}