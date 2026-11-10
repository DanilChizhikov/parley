using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal sealed class CodexEventMapper
    {
        private const string BashTool = "Bash";
        private const string WebSearchTool = "WebSearch";
        private const string ViewImageTool = "ViewImage";
        private const string TodoWriteTool = "TodoWrite";
        private const string AgentTool = "Agent";
        private const string SpawnAgent = "spawnAgent";
        private const int AgentDescriptionLength = 60;

        private readonly IAgentSink _sink;
        private readonly Dictionary<string, JObject> _items = new ();
        private readonly Dictionary<string, long> _summaryParts = new ();
        private readonly Dictionary<string, string> _spawnByThread = new ();
        private readonly Dictionary<string, string> _agentReplies = new ();
        private readonly HashSet<string> _finishedAgents = new ();

        public string PlanText { get; private set; }
        public string PlanItemId { get; private set; }

        private JObject _lastTotal;
        private JObject _turnStartTotal;
        private int _todoCounter;

        public CodexEventMapper(IAgentSink sink)
        {
            _sink = sink;
        }

        public JObject FindItem(string itemId)
        {
            return itemId != null && _items.TryGetValue(itemId, out JObject item) ? item : null;
        }

        public void ClearPlan()
        {
            PlanText = null;
            PlanItemId = null;
        }

        public void Handle(string method, JObject parameters)
        {
            string parent = SpawnOf((string)parameters["threadId"]);
            if (parent != null)
            {
                HandleSubagent(method, parameters, parent);
                return;
            }

            switch (method)
            {
                case "turn/started":
                    _turnStartTotal = _lastTotal;
                    ClearPlan();
                    break;
                case "item/started":
                    HandleItemStarted(parameters["item"] as JObject, null);
                    break;
                case "item/agentMessage/delta":
                case "item/reasoning/textDelta":
                    _sink.TextDelta((string)parameters["itemId"], (string)parameters["delta"]);
                    break;
                case "item/reasoning/summaryTextDelta":
                    HandleSummaryDelta(parameters);
                    break;
                case "item/completed":
                    HandleItemCompleted(parameters["item"] as JObject, null);
                    break;
                case "turn/plan/updated":
                    HandlePlanUpdated(parameters);
                    break;
                case "thread/tokenUsage/updated":
                    HandleTokenUsage(parameters["tokenUsage"] as JObject);
                    break;
                case "turn/completed":
                    HandleTurnCompleted(parameters["turn"] as JObject);
                    break;
                case "error":
                    HandleError(parameters);
                    break;
                case "warning":
                case "configWarning":
                    _sink.Notice(NoticeLevel.Warning, (string)parameters["message"] ?? (string)parameters["summary"] ?? parameters.ToString());
                    break;
                case "deprecationNotice":
                    _sink.Notice(NoticeLevel.Info, (string)parameters["summary"] ?? (string)parameters["message"] ?? parameters.ToString());
                    break;
                case CodexRateLimits.UpdatedNotification:
                    RateLimits limits = CodexRateLimits.FromSnapshot(parameters["rateLimits"] as JObject);
                    if (limits != null)
                    {
                        _sink.RateLimitsChanged(limits);
                    }

                    break;
                case "model/rerouted":
                    _sink.Notice(NoticeLevel.Info, "Codex switched the model to " + ((string)parameters["toModel"] ?? "another model") + ".");
                    break;
            }
        }

        private static bool IsSpawn(JObject item)
        {
            return (string)item["type"] == "collabAgentToolCall" && (string)item["tool"] == SpawnAgent;
        }

        private static string ToolName(JObject item)
        {
            switch ((string)item["type"])
            {
                case "collabAgentToolCall":
                    return IsSpawn(item) ? AgentTool : null;
                case "commandExecution":
                    return BashTool;
                case "fileChange":
                    return CodexApprovals.ApplyPatchTool;
                case "mcpToolCall":
                    return "mcp__" + (string)item["server"] + "__" + (string)item["tool"];
                case "dynamicToolCall":
                    return CodexToolBridge.DisplayName((string)item["tool"]);
                case "webSearch":
                    return WebSearchTool;
                case "imageView":
                    return ViewImageTool;
                case "plan":
                    return PendingRequest.ExitPlanModeTool;
                default:
                    return null;
            }
        }

        private static JObject ToolInput(JObject item)
        {
            switch ((string)item["type"])
            {
                case "commandExecution":
                    return new JObject { ["command"] = (string)item["command"], ["cwd"] = (string)item["cwd"] };
                case "fileChange":
                    return new JObject { ["changes"] = item["changes"]?.DeepClone() ?? new JArray() };
                case "mcpToolCall":
                case "dynamicToolCall":
                    return item["arguments"] as JObject ?? new JObject();
                case "webSearch":
                    return new JObject { ["query"] = (string)item["query"] };
                case "imageView":
                    return new JObject { ["path"] = (string)item["path"] };
                case "plan":
                    return new JObject { ["plan"] = (string)item["text"] };
                case "collabAgentToolCall":
                    string prompt = (string)item["prompt"] ?? string.Empty;
                    return new JObject { ["description"] = Describe(prompt), ["prompt"] = prompt, ["model"] = (string)item["model"] };
                default:
                    return new JObject();
            }
        }

        private static string Describe(string prompt)
        {
            string line = prompt.Trim();
            int newline = line.IndexOf('\n');
            if (newline >= 0)
            {
                line = line.Substring(0, newline).Trim();
            }

            return line.Length > AgentDescriptionLength ? line.Substring(0, AgentDescriptionLength - 1) + "…" : line;
        }

        private static string Join(JToken lines, string separator)
        {
            List<string> parts = new ();
            if (lines is JArray array)
            {
                foreach (JToken line in array)
                {
                    string text = line.Type == JTokenType.String ? (string)line : (string)line["text"];
                    if (!string.IsNullOrEmpty(text))
                    {
                        parts.Add(text);
                    }
                }
            }

            return string.Join(separator, parts);
        }

        private static string CommandOutput(JObject item)
        {
            string output = (string)item["aggregatedOutput"] ?? string.Empty;
            if ((string)item["status"] == "declined" && output.Length == 0)
            {
                output = "The user declined this command.";
            }

            long? exitCode = (long?)item["exitCode"];
            if (exitCode.HasValue && exitCode.Value != 0)
            {
                output = output.TrimEnd() + "\n[exit code " + exitCode.Value + "]";
            }

            return output;
        }

        private static string FileChangeOutput(JObject item)
        {
            StringBuilder builder = new StringBuilder();
            if (item["changes"] is JArray changes)
            {
                foreach (JToken change in changes)
                {
                    builder.Append((string)(change["kind"] as JObject)?["type"] ?? "update").Append(' ').AppendLine((string)change["path"]);
                }
            }

            builder.Append("Status: ").Append((string)item["status"]);
            return builder.ToString();
        }

        private static string McpOutput(JObject item)
        {
            string error = (string)(item["error"] as JObject)?["message"];
            return string.IsNullOrEmpty(error) ? Join((item["result"] as JObject)?["content"], "\n") : error;
        }

        private static bool IsFailed(JObject item)
        {
            string status = (string)item["status"];
            if (status == "failed" || status == "declined" || (bool?)item["success"] == false)
            {
                return true;
            }

            long? exitCode = (long?)item["exitCode"];
            return exitCode.HasValue && exitCode.Value != 0;
        }

        private static string ErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || message[0] != '{')
            {
                return message;
            }

            try
            {
                JObject json = JObject.Parse(message);
                JToken nested = json["error"];
                string text = nested?.Type == JTokenType.Object ? (string)nested["message"] : (string)json["message"];
                return string.IsNullOrEmpty(text) ? message : text;
            }
            catch (JsonException)
            {
                return message;
            }
        }

        private static long Tokens(JObject usage, string field)
        {
            return (long?)usage?[field] ?? 0;
        }

        private string SpawnOf(string threadId)
        {
            return threadId != null && _spawnByThread.TryGetValue(threadId, out string spawnId) ? spawnId : null;
        }

        private void RegisterSpawn(JObject item)
        {
            if (!IsSpawn(item) || item["receiverThreadIds"] is not JArray threads)
            {
                return;
            }

            foreach (JToken thread in threads)
            {
                _spawnByThread[(string)thread] = (string)item["id"];
            }
        }

        private void HandleSubagent(string method, JObject parameters, string spawnId)
        {
            switch (method)
            {
                case "item/started":
                    HandleItemStarted(parameters["item"] as JObject, spawnId);
                    break;
                case "item/agentMessage/delta":
                case "item/reasoning/textDelta":
                    _sink.TextDelta((string)parameters["itemId"], (string)parameters["delta"]);
                    break;
                case "item/reasoning/summaryTextDelta":
                    HandleSummaryDelta(parameters);
                    break;
                case "item/completed":
                    HandleItemCompleted(parameters["item"] as JObject, spawnId);
                    break;
                case "turn/completed":
                    string status = (string)(parameters["turn"] as JObject)?["status"];
                    FinishAgent(spawnId, status == "failed" || status == "interrupted", null);
                    break;
            }
        }

        private void FinishAgent(string spawnId, bool failed, string message)
        {
            if (!_finishedAgents.Add(spawnId))
            {
                return;
            }

            _agentReplies.TryGetValue(spawnId, out string reply);
            string text = string.IsNullOrEmpty(message) ? reply : message;
            _sink.ToolResult(new ToolResultRequest(spawnId, text ?? string.Empty, failed, null));
        }

        private void ResolveAgentStates(JObject item)
        {
            if (item["agentsStates"] is not JObject states)
            {
                return;
            }

            foreach (JProperty state in states.Properties())
            {
                string spawnId = SpawnOf(state.Name);
                string status = (string)state.Value["status"];
                if (spawnId == null || status == "pendingInit" || status == "running")
                {
                    continue;
                }

                FinishAgent(spawnId, status != "completed" && status != "shutdown", (string)state.Value["message"]);
            }
        }

        private void HandleItemStarted(JObject item, string parent)
        {
            if (item == null)
            {
                return;
            }

            string id = (string)item["id"];
            _items[id] = item;
            RegisterSpawn(item);
            switch ((string)item["type"])
            {
                case "subAgentActivity":
                    HandleAgentActivity(item, parent, false);
                    break;
                case "agentMessage":
                    _sink.BlockStarted(new BlockStartRequest(id, BlockKind.Text, parent, null, null));
                    break;
                case "reasoning":
                    _sink.BlockStarted(new BlockStartRequest(id, BlockKind.Thinking, parent, null, null));
                    break;
                default:
                    string toolName = ToolName(item);
                    if (toolName != null)
                    {
                        _sink.BlockStarted(new BlockStartRequest(id, BlockKind.ToolUse, parent, id, toolName));
                    }

                    break;
            }
        }

        private void HandleSummaryDelta(JObject parameters)
        {
            string itemId = (string)parameters["itemId"];
            long index = (long?)parameters["summaryIndex"] ?? 0;
            if (_summaryParts.TryGetValue(itemId, out long previous) && index != previous)
            {
                _sink.TextDelta(itemId, "\n\n");
            }

            _summaryParts[itemId] = index;
            _sink.TextDelta(itemId, (string)parameters["delta"]);
        }

        private void HandleItemCompleted(JObject item, string parent)
        {
            if (item == null)
            {
                return;
            }

            string id = (string)item["id"];
            _items[id] = item;
            _summaryParts.Remove(id);
            string type = (string)item["type"];
            switch (type)
            {
                case "agentMessage":
                    if (parent != null)
                    {
                        _agentReplies[parent] = (string)item["text"];
                    }

                    _sink.BlockFinalized(new BlockFinalizeRequest(id, BlockKind.Text, parent, (string)item["text"], null, null, null));
                    return;
                case "reasoning":
                    string summary = Join(item["summary"], "\n\n");
                    string thinking = summary.Length > 0 ? summary : Join(item["content"], "\n\n");
                    _sink.BlockFinalized(new BlockFinalizeRequest(id, BlockKind.Thinking, parent, thinking, null, null, null));
                    return;
                case "contextCompaction":
                    _sink.Notice(NoticeLevel.Info, "Conversation compacted.");
                    return;
                case "enteredReviewMode":
                case "exitedReviewMode":
                    _sink.Notice(NoticeLevel.Info, (string)item["review"]);
                    return;
                case "plan":
                    PlanText = (string)item["text"];
                    PlanItemId = id;
                    break;
                case "collabAgentToolCall":
                    HandleCollabCompleted(item, parent);
                    return;
                case "subAgentActivity":
                    HandleAgentActivity(item, parent, true);
                    return;
            }

            string toolName = ToolName(item);
            if (toolName == null)
            {
                return;
            }

            _sink.BlockFinalized(new BlockFinalizeRequest(id, BlockKind.ToolUse, parent, null, id, toolName, ToolInput(item)));
            string output = type switch
            {
                "commandExecution" => CommandOutput(item),
                "fileChange" => FileChangeOutput(item),
                "mcpToolCall" => McpOutput(item),
                "dynamicToolCall" => Join(item["contentItems"], "\n"),
                "webSearch" => "Searched the web: " + (string)item["query"],
                "plan" => "Plan ready for review.",
                _ => string.Empty,
            };

            JToken structured = type == "mcpToolCall" ? (item["result"] as JObject)?["structuredContent"] : null;
            _sink.ToolResult(new ToolResultRequest(id, output, IsFailed(item), structured));
        }

        private void HandleAgentActivity(JObject item, string parent, bool completed)
        {
            string thread = (string)item["agentThreadId"];
            if (string.IsNullOrEmpty(thread))
            {
                return;
            }

            switch ((string)item["kind"])
            {
                case "started":
                    string id = (string)item["id"];
                    _spawnByThread[thread] = id;
                    if (!completed)
                    {
                        _sink.BlockStarted(new BlockStartRequest(id, BlockKind.ToolUse, parent, id, AgentTool));
                        return;
                    }

                    JObject input = new JObject { ["description"] = AgentName((string)item["agentPath"]), ["model"] = (string)item["model"] };
                    _sink.BlockFinalized(new BlockFinalizeRequest(id, BlockKind.ToolUse, parent, null, id, AgentTool, input));
                    break;
                case "completed" when completed:
                    FinishAgentOf(thread, false, null);
                    break;
                case "interrupted" when completed:
                    FinishAgentOf(thread, true, "Interrupted.");
                    break;
            }
        }

        private static string AgentName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string trimmed = path.TrimEnd('/');
            return trimmed.Substring(trimmed.LastIndexOf('/') + 1);
        }

        private void FinishAgentOf(string thread, bool failed, string message)
        {
            string spawnId = SpawnOf(thread);
            if (spawnId != null)
            {
                FinishAgent(spawnId, failed, message);
            }
        }

        private void HandleCollabCompleted(JObject item, string parent)
        {
            if (IsSpawn(item))
            {
                RegisterSpawn(item);
                string id = (string)item["id"];
                _sink.BlockFinalized(new BlockFinalizeRequest(id, BlockKind.ToolUse, parent, null, id, AgentTool, ToolInput(item)));
                if (IsFailed(item))
                {
                    FinishAgent(id, true, "Codex could not start the agent.");
                }
            }

            ResolveAgentStates(item);
        }

        private void HandlePlanUpdated(JObject parameters)
        {
            JArray todos = new JArray();
            if (parameters["plan"] is JArray steps)
            {
                foreach (JToken step in steps)
                {
                    string status = (string)step["status"];
                    todos.Add(new JObject
                    {
                        ["content"] = (string)step["step"],
                        ["status"] = status == "inProgress" ? "in_progress" : status,
                        ["activeForm"] = (string)step["step"],
                    });
                }
            }

            string toolUseId = "codex_todo_" + (++_todoCounter);
            JObject input = new JObject { ["todos"] = todos };
            _sink.BlockFinalized(new BlockFinalizeRequest(toolUseId, BlockKind.ToolUse, null, null, toolUseId, TodoWriteTool, input));
            _sink.ToolResult(new ToolResultRequest(toolUseId, "Plan updated.", false, null));
        }

        private void HandleTokenUsage(JObject usage)
        {
            if (usage == null)
            {
                return;
            }

            JObject total = usage["total"] as JObject;
            if (Tokens(total, "totalTokens") != Tokens(_lastTotal, "totalTokens"))
            {
                ReportUsage(usage["last"] as JObject);
            }

            _lastTotal = total;
            long window = (long?)usage["modelContextWindow"] ?? 0;
            if (window > 0)
            {
                _sink.ContextUsage(Tokens(usage["last"] as JObject, "totalTokens"), window);
            }
        }

        private void ReportUsage(JObject last)
        {
            if (last == null)
            {
                return;
            }

            long cached = Tokens(last, "cachedInputTokens");
            _sink.UsageReported(new UsageReport(
                null,
                null,
                null,
                Math.Max(0, Tokens(last, "inputTokens") - cached),
                0,
                cached,
                Tokens(last, "outputTokens"),
                Tokens(last, "reasoningOutputTokens")));
        }

        private void HandleTurnCompleted(JObject turn)
        {
            string status = (string)turn?["status"];
            TurnResult result = new TurnResult
            {
                IsError = status == "failed",
                Subtype = status switch
                {
                    "failed" => "error",
                    "interrupted" => "interrupted",
                    _ => "success",
                },
                DurationMs = (long?)turn?["durationMs"] ?? 0,
                NumTurns = 1,
                InputTokens = Tokens(_lastTotal, "inputTokens") - Tokens(_turnStartTotal, "inputTokens"),
                OutputTokens = Tokens(_lastTotal, "outputTokens") - Tokens(_turnStartTotal, "outputTokens"),
                CacheReadTokens = Tokens(_lastTotal, "cachedInputTokens") - Tokens(_turnStartTotal, "cachedInputTokens"),
            };

            string error = ErrorMessage((string)(turn?["error"] as JObject)?["message"]);
            if (!string.IsNullOrEmpty(error))
            {
                result.Errors.Add(error);
            }

            if (result.IsError)
            {
                _sink.Notice(NoticeLevel.Error, "Turn ended with an error" + (string.IsNullOrEmpty(error) ? "." : ": " + error));
            }
            else if (status == "interrupted")
            {
                _sink.Notice(NoticeLevel.Info, "Interrupted.");
            }

            _items.Clear();
            _summaryParts.Clear();
            _sink.TurnCompleted(result);
        }

        private void HandleError(JObject parameters)
        {
            JObject error = parameters["error"] as JObject;
            string message = ErrorMessage((string)error?["message"]) ?? "Codex reported an error.";
            JToken info = error?["codexErrorInfo"];
            if (info != null && info.Type == JTokenType.String && (string)info == "unauthorized")
            {
                _sink.AuthRequired("Codex reports: " + message + ". Sign in again or pick another profile.");
                return;
            }

            if ((bool?)parameters["willRetry"] == true)
            {
                _sink.Notice(NoticeLevel.Warning, "Retrying: " + message);
            }
        }
    }
}