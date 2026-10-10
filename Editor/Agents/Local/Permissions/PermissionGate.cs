using System;
using System.Collections.Generic;
using System.IO;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
	internal sealed class PermissionGate
	{
		public const string FileEditRuleTool = "FileEdit";

		private const string AnyPattern = "*";
		private const string GitFolderSegment = "/.git/";
		private const string GitFolderSuffix = "/.git";

		private static readonly string[] SafeCommands =
		{
			"ls", "pwd", "cat", "head", "tail", "wc", "grep", "which", "echo", "stat", "du", "df",
			"git status", "git diff", "git log", "git show", "git rev-parse", "git blame",
		};

		private static readonly string[] UnsafeOptions = { "--output", "--ext-diff", "--textconv", "--no-index" };

		private static readonly char[] ShellMetaCharacters = { ';', '&', '|', '>', '<', '`', '$', '\n', '\r' };

		private static readonly char[] SafeCommandForbiddenCharacters = { '{', '}', '\\' };

		private static readonly char[] PathSeparators = { '/', '\\' };

		private static readonly char[] TokenSeparators = { ' ', '\t' };

		private static readonly HashSet<string> CommandRunners = new (StringComparer.OrdinalIgnoreCase)
		{
			"bash", "sh", "zsh", "fish", "dash", "cmd", "pwsh", "powershell", "python", "python3", "py", "node", "deno", "bun",
			"ruby", "perl", "php", "osascript", "env", "xargs", "sudo", "nohup", "exec", "time",
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
			if (string.IsNullOrWhiteSpace(command)
				|| command.IndexOfAny(ShellMetaCharacters) >= 0
				|| command.IndexOfAny(SafeCommandForbiddenCharacters) >= 0)
			{
				return false;
			}

			string trimmed = command.Trim();
			bool listed = false;
			foreach (string safe in SafeCommands)
			{
				if (trimmed == safe || trimmed.StartsWith(safe + " ", StringComparison.Ordinal))
				{
					listed = true;
					break;
				}
			}

			if (!listed)
			{
				return false;
			}

			foreach (string argument in CommandLine.Split(trimmed))
			{
				if (HasUnsafeOption(argument))
				{
					return false;
				}
			}

			return true;
		}

		public static AllowRule SuggestRule(IParleyTool tool, JObject input, string projectRoot)
		{
			string pattern = AnyPattern;
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
					pattern = Uri.TryCreate(ToolInput.String(input, "url", string.Empty), UriKind.Absolute, out Uri uri) ? uri.Host : AnyPattern;
					break;
			}

			return new AllowRule(projectRoot, ruleTool, pattern);
		}

		public static string CommandPrefix(string command)
		{
			string trimmed = command.Trim();
			string[] tokens = trimmed.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 0)
			{
				return AnyPattern;
			}

			if (CommandRunners.Contains(ExecutableName(tokens[0])))
			{
				return trimmed;
			}

			if (tokens.Length > 1 && !tokens[1].StartsWith("-") && !tokens[1].Contains("/") && !tokens[1].Contains("."))
			{
				return tokens[0] + " " + tokens[1];
			}

			return tokens[0];
		}

		public static string DescribeRule(AllowRule rule)
		{
			return rule.Tool + (rule.Pattern == AnyPattern ? string.Empty : "(" + rule.Pattern + ")");
		}

		public static JArray ToSuggestions(AllowRule rule)
		{
			return new JArray
			{
				new JObject
				{
					["type"] = "addRules",
					["destination"] = "localSettings",
					["rules"] = new JArray
					{
						new JObject
						{
							["toolName"] = rule.Tool,
							["ruleContent"] = rule.Pattern == AnyPattern ? null : rule.Pattern,
						},
					},
				},
			};
		}

		public GateResult Evaluate(IParleyTool tool, JObject input, PermissionMode mode)
		{
			string path = tool.TargetPath(input);
			bool outside = path != null && !IsInsideWorkspace(path);
			bool gitInternal = path != null && IsInsideGitFolder(path);
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

			string reason = outside ? "Path is outside the project: " + path : gitInternal ? "Path is inside a .git folder: " + path : null;
			GateResult ask = mode == PermissionMode.DontAsk
				? new GateResult(GateVerdict.Deny, "Don't ask mode denies actions that need approval.")
				: new GateResult(GateVerdict.Ask, reason);

			switch (tool.Kind)
			{
				case ToolKind.ReadOnly:
					return outside ? ask : new GateResult(GateVerdict.Allow, null);
				case ToolKind.FileEdit:
					if (!outside && !gitInternal && (mode == PermissionMode.AcceptEdits || HasRule(FileEditRuleTool, AnyPattern)))
					{
						return new GateResult(GateVerdict.Allow, null);
					}

					return ask;
				case ToolKind.Execute:
					string command = ToolInput.String(input, "command", string.Empty);
					bool safe = IsSafeCommand(command) && ArgumentsInsideWorkspace(command);
					return safe || MatchesCommandRule(tool.Name, command) ? new GateResult(GateVerdict.Allow, null) : ask;
				case ToolKind.Network:
					Uri.TryCreate(ToolInput.String(input, "url", string.Empty), UriKind.Absolute, out Uri uri);
					return uri != null && HasRule(tool.Name, uri.Host) ? new GateResult(GateVerdict.Allow, null) : ask;
				default:
					return HasRule(tool.Name, AnyPattern) ? new GateResult(GateVerdict.Allow, null) : ask;
			}
		}

		private static bool HasUnsafeOption(string argument)
		{
			foreach (string option in UnsafeOptions)
			{
				if (argument.StartsWith(option, StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}

		private static string ExecutableName(string token)
		{
			int separator = token.LastIndexOfAny(PathSeparators);
			string name = separator < 0 ? token : token.Substring(separator + 1);
			return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
		}

		private static bool IsInsideGitFolder(string path)
		{
			string normalized = ProjectPaths.Normalize(path);
			return normalized.IndexOf(GitFolderSegment, StringComparison.OrdinalIgnoreCase) >= 0
				|| normalized.EndsWith(GitFolderSuffix, StringComparison.OrdinalIgnoreCase);
		}

		private static string ResolveArgument(string argument)
		{
			try
			{
				return ProjectPaths.Resolve(argument);
			}
			catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
			{
				return null;
			}
		}

		private bool IsInsideWorkspace(string path)
		{
			return ProjectPaths.IsInsideWorkspace(path, _projectRoot, _extraDirectories());
		}

		private bool ArgumentsInsideWorkspace(string command)
		{
			int position = 0;
			bool git = false;
			foreach (string argument in CommandLine.Split(command))
			{
				int index = position++;
				if (index == 0)
				{
					git = argument == "git";
					continue;
				}

				if ((git && index == 1) || argument.Length == 0 || argument[0] == '-')
				{
					continue;
				}

				if (argument[0] == '~')
				{
					return false;
				}

				string resolved = ResolveArgument(argument);
				if (resolved == null || !IsInsideWorkspace(resolved))
				{
					return false;
				}
			}

			return true;
		}

		private bool HasRule(string tool, string pattern)
		{
			foreach (AllowRule rule in _rules())
			{
				if (rule.Tool == tool && (rule.Pattern == AnyPattern || rule.Pattern == pattern))
				{
					return true;
				}
			}

			return false;
		}

		private bool MatchesCommandRule(string tool, string command)
		{
			if (command.IndexOfAny(ShellMetaCharacters) >= 0)
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
