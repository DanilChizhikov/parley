using System;
using System.IO;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal static class ToolPresenter
    {
        public const int MaxOutputChars = 6000;

        private const int MaxErrorChars = 2000;
        private const int MaxSummaryChars = 120;
        private const int MaxFallbackSummaryChars = 80;
        private const string McpPrefix = "mcp__";
        private const string ViewImageTool = "ViewImage";

        public static string DisplayName(string toolName)
        {
            if (string.IsNullOrEmpty(toolName))
            {
                return "Tool";
            }

            if (toolName.StartsWith(SdkMcpBridge.ToolPrefix, StringComparison.Ordinal))
            {
                return "Unity · " + toolName.Substring(SdkMcpBridge.ToolPrefix.Length);
            }

            if (toolName.StartsWith(ToolCatalog.UnityLocalPrefix, StringComparison.Ordinal))
            {
                return "Unity · " + toolName.Substring(ToolCatalog.UnityLocalPrefix.Length);
            }

            if (toolName.StartsWith(McpPrefix, StringComparison.Ordinal))
            {
                string[] parts = toolName.Substring(McpPrefix.Length).Split(new[] { "__" }, 2, StringSplitOptions.None);
                return parts.Length == 2 ? parts[0] + " · " + parts[1] : toolName;
            }

            return toolName;
        }

        public static string Summary(TranscriptBlock block)
        {
            JObject input = block.Input;
            if (input == null)
            {
                return string.Empty;
            }

            switch (block.ToolName)
            {
                case "Bash":
                    return First(Str(input, "description"), Str(input, "command"));
                case "Read":
                case "Write":
                case "Edit":
                case "MultiEdit":
                case "NotebookEdit":
                    return Relative(First(Str(input, "file_path"), Str(input, "notebook_path")));
                case "Glob":
                case "Grep":
                    string path = Str(input, "path");
                    return Str(input, "pattern") + (string.IsNullOrEmpty(path) ? string.Empty : "  in " + Relative(path));
                case "WebFetch":
                    return Str(input, "url");
                case "WebSearch":
                    return Str(input, "query");
                case "Agent":
                case "Task":
                    return First(Str(input, "description"), Str(input, "subagent_type"));
                case "TodoWrite":
                    return input["todos"] is JArray todos ? todos.Count + " items" : string.Empty;
                case "TaskCreate":
                    return Str(input, "subject");
                case "TaskUpdate":
                    return First(Str(input, "taskId"), Str(input, "id")) + " → " + Str(input, "status");
                case PendingRequest.AskUserQuestionTool:
                    return input["questions"] is JArray questions ? questions.Count + " question(s)" : string.Empty;
                case PendingRequest.ExitPlanModeTool:
                    return "Plan ready for review";
                case "Skill":
                    return First(Str(input, "skill"), Str(input, "command"));
                case CodexApprovals.ApplyPatchTool:
                    return ChangesSummary(input["changes"] as JArray);
                case ViewImageTool:
                    return Relative(Str(input, "path"));
                default:
                    return FirstString(input);
            }
        }

        public static bool ExpandedByDefault(TranscriptBlock block)
        {
            if (block.IsError)
            {
                return true;
            }

            switch (block.ToolName)
            {
                case "Edit":
                case "MultiEdit":
                case "Write":
                case "Agent":
                case "Task":
                case PendingRequest.ExitPlanModeTool:
                case PendingRequest.AskUserQuestionTool:
                case CodexApprovals.ApplyPatchTool:
                    return true;
                default:
                    return false;
            }
        }

        public static VisualElement BuildBody(TranscriptBlock block)
        {
            VisualElement body = new VisualElement();
            body.AddToClassList("pl-tool__content");
            JObject input = block.Input ?? new JObject();
            switch (block.ToolName)
            {
                case "Bash":
                    body.Add(MarkdownView.CodeBlock("$ " + Str(input, "command"), "bash"));
                    AddOutput(body, block, "text");
                    break;
                case "Edit":
                    body.Add(new DiffView(Str(input, "old_string"), Str(input, "new_string"), Relative(Str(input, "file_path"))));
                    AddErrorOnly(body, block);
                    break;
                case "MultiEdit":
                    body.Add(MultiEdit(input["edits"] as JArray));
                    AddErrorOnly(body, block);
                    break;
                case "Write":
                    body.Add(WritePreview(input, OriginalFile(block.StructuredResult)));
                    AddErrorOnly(body, block);
                    break;
                case "Read":
                    AddOutput(body, block, LanguageOf(Str(input, "file_path")));
                    break;
                case "TodoWrite":
                    body.Add(TodoList(input["todos"] as JArray));
                    break;
                case PendingRequest.ExitPlanModeTool:
                    AddMarkdown(body, Str(input, "plan"));
                    AddResultMarkdown(body, block);
                    break;
                case PendingRequest.AskUserQuestionTool:
                case "Agent":
                case "Task":
                    AddResultMarkdown(body, block);
                    break;
                case CodexApprovals.ApplyPatchTool:
                    body.Add(PatchChanges(input["changes"] as JArray));
                    AddErrorOnly(body, block);
                    break;
                default:
                    if (input.Count > 0)
                    {
                        body.Add(MarkdownView.CodeBlock(Truncate(input.ToString(Formatting.Indented), MaxOutputChars), "json"));
                    }

                    AddOutput(body, block, "text");
                    break;
            }

            return body;
        }

        public static VisualElement MultiEdit(JArray edits)
        {
            VisualElement list = new VisualElement();
            if (edits == null)
            {
                return list;
            }

            foreach (JToken edit in edits)
            {
                list.Add(new DiffView((string)edit["old_string"], (string)edit["new_string"]));
            }

            return list;
        }

        public static VisualElement WritePreview(JObject input, string originalText)
        {
            string path = Str(input, "file_path");
            string content = Str(input, "content");
            if (originalText != null)
            {
                return new DiffView(originalText, content, Relative(path));
            }

            return MarkdownView.CodeBlock(Truncate(content, MaxOutputChars), LanguageOf(path));
        }

        public static VisualElement PatchChanges(JArray changes)
        {
            VisualElement list = new VisualElement();
            if (changes == null)
            {
                return list;
            }

            foreach (JToken change in changes)
            {
                VisualElement header = new VisualElement();
                header.AddToClassList("pl-diff__header");
                header.Add(ParleyStyles.Text(Relative((string)change["path"]), "pl-diff__title"));
                header.Add(ParleyStyles.Text(ChangeKind(change), ParleyStyles.Muted));
                list.Add(header);
                string diff = (string)change["diff"];
                if (!string.IsNullOrEmpty(diff))
                {
                    list.Add(MarkdownView.CodeBlock(Truncate(diff, MaxOutputChars), "diff"));
                }
            }

            return list;
        }

        public static string LanguageOf(string path)
        {
            string extension = string.IsNullOrEmpty(path) ? string.Empty : Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".cs" => "csharp",
                ".json" or ".asmdef" or ".asmref" => "json",
                ".sh" or ".zsh" or ".bash" => "bash",
                ".js" or ".ts" => "javascript",
                ".shader" or ".hlsl" or ".cginc" or ".compute" => "hlsl",
                ".md" => "markdown",
                ".uss" or ".css" => "css",
                ".uxml" or ".xml" => "xml",
                ".yaml" or ".yml" or ".asset" or ".prefab" or ".unity" or ".meta" => "yaml",
                _ => "text",
            };
        }

        public static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, max) + "\n… (" + (text.Length - max) + " more characters)";
        }

        public static string Relative(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : ProjectPaths.ToProjectRelative(path);
        }

        public static Label TodoLabel(string status, string text)
        {
            string normalized = status ?? "pending";
            string icon = normalized == "completed" ? "●" : normalized == "in_progress" ? "◐" : "○";
            Label label = ParleyStyles.Text(icon + "  " + (text ?? string.Empty), "pl-todo");
            label.AddToClassList("pl-todo--" + normalized.Replace('_', '-'));
            return label;
        }

        private static string OriginalFile(JToken structuredResult)
        {
            JToken original = (structuredResult as JObject)?["originalFile"];
            return original != null && original.Type == JTokenType.String ? (string)original : null;
        }

        private static string ChangesSummary(JArray changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return string.Empty;
            }

            string first = Relative((string)changes[0]["path"]);
            return changes.Count == 1 ? first : first + " +" + (changes.Count - 1) + " more";
        }

        private static string ChangeKind(JToken change)
        {
            JToken kind = change["kind"];
            if (kind == null)
            {
                return string.Empty;
            }

            if (kind.Type == JTokenType.String)
            {
                return (string)kind;
            }

            return (string)kind["type"] ?? string.Empty;
        }

        private static VisualElement TodoList(JArray todos)
        {
            VisualElement list = new VisualElement();
            list.AddToClassList("pl-todos");
            if (todos == null)
            {
                return list;
            }

            foreach (JToken todo in todos)
            {
                list.Add(TodoLabel((string)todo["status"], (string)todo["content"]));
            }

            return list;
        }

        private static void AddOutput(VisualElement body, TranscriptBlock block, string language)
        {
            if (block.Result == null)
            {
                if (!block.IsFinished)
                {
                    body.Add(ParleyStyles.Text("Running…", ParleyStyles.Muted));
                }

                return;
            }

            string output = block.Result.Length == 0 ? "(no output)" : Truncate(block.Result, MaxOutputChars);
            VisualElement code = MarkdownView.CodeBlock(output, block.IsError ? "error" : language, !block.IsError && language != "text");
            code.EnableInClassList("pl-md-code--error", block.IsError);
            body.Add(code);
        }

        private static void AddErrorOnly(VisualElement body, TranscriptBlock block)
        {
            if (block.IsError && !string.IsNullOrEmpty(block.Result))
            {
                body.Add(ParleyStyles.Text(Truncate(block.Result, MaxErrorChars), "pl-tool__error"));
            }
        }

        private static void AddMarkdown(VisualElement body, string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return;
            }

            MarkdownView view = new MarkdownView();
            view.SetMarkdown(markdown);
            body.Add(view);
        }

        private static void AddResultMarkdown(VisualElement body, TranscriptBlock block)
        {
            if (string.IsNullOrEmpty(block.Result))
            {
                return;
            }

            if (block.IsError)
            {
                body.Add(ParleyStyles.Text(Truncate(block.Result, MaxErrorChars * 2), "pl-tool__error"));
                return;
            }

            VisualElement result = new VisualElement();
            result.AddToClassList("pl-tool__result");
            AddMarkdown(result, Truncate(block.Result, MaxOutputChars * 2));
            body.Add(result);
        }

        private static string FirstString(JObject input)
        {
            foreach (JProperty property in input.Properties())
            {
                if (property.Value.Type == JTokenType.String)
                {
                    return Truncate((string)property.Value, MaxFallbackSummaryChars);
                }
            }

            return string.Empty;
        }

        private static string Str(JObject input, string name)
        {
            JToken token = input?[name];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            return token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
        }

        private static string First(string first, string second)
        {
            return Truncate(string.IsNullOrWhiteSpace(first) ? second ?? string.Empty : first, MaxSummaryChars).Replace('\n', ' ');
        }
    }
}