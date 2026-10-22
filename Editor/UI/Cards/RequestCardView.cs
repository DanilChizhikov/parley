using System;
using System.Collections.Generic;
using System.IO;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Sessions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class RequestCardView : BlockView
    {
        private readonly ChatSession _session;

        private bool _resolvedRendered;

        public RequestCardView(TranscriptBlock block, ChatSession session) : base(block)
        {
            _session = session;
            AddToClassList("pl-request");
            Refresh();
        }

        public static string DescribeSuggestions(JArray suggestions)
        {
            if (suggestions == null || suggestions.Count == 0)
            {
                return null;
            }

            List<string> parts = new List<string>();
            foreach (JToken suggestion in suggestions)
            {
                string destination = (string)suggestion["destination"];
                string where = destination switch
                {
                    "session" => "this session",
                    "localSettings" => "this project (local)",
                    "projectSettings" => "this project (shared)",
                    "userSettings" => "all projects",
                    _ => destination,
                };

                switch ((string)suggestion["type"])
                {
                    case "addRules":
                    case "replaceRules":
                        if (suggestion["rules"] is JArray rules)
                        {
                            foreach (JToken rule in rules)
                            {
                                string content = (string)rule["ruleContent"];
                                parts.Add((string)rule["toolName"] + (string.IsNullOrEmpty(content) ? string.Empty : "(" + content + ")") + " · " + where);
                            }
                        }

                        break;
                    case "setMode":
                        parts.Add("switch to " + PermissionModes.DisplayName(PermissionModes.FromWire((string)suggestion["mode"])) + " · " + where);
                        break;
                    case "addDirectories":
                        if (suggestion["directories"] is JArray directories)
                        {
                            parts.Add("access " + string.Join(", ", directories.ToObject<string[]>()) + " · " + where);
                        }

                        break;
                }
            }

            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        public override void Refresh()
        {
            if (!string.IsNullOrEmpty(Block.Resolution))
            {
                if (!_resolvedRendered)
                {
                    _resolvedRendered = true;
                    Clear();
                    BuildResolved();
                }

                return;
            }

            if (childCount > 0)
            {
                return;
            }

            PendingRequest request = _session.FindRequest(Block.RequestId);
            if (request == null)
            {
                Block.Resolution = "No longer pending (the agent restarted)";
                Refresh();
                return;
            }

            switch (request.Kind)
            {
                case RequestKind.Question:
                    AddToClassList("pl-request--question");
                    Add(new QuestionCard(request, Respond));
                    break;
                case RequestKind.PlanApproval:
                    AddToClassList("pl-request--plan");
                    BuildPlan(request);
                    break;
                case RequestKind.EnterPlan:
                    AddToClassList("pl-request--plan");
                    BuildEnterPlan(request);
                    break;
                default:
                    AddToClassList("pl-request--permission");
                    BuildPermission(request);
                    break;
            }
        }

        private static VisualElement BuildPreview(PendingRequest request)
        {
            JObject input = request.Input ?? new JObject();
            switch (request.ToolName)
            {
                case "Bash":
                    return MarkdownView.CodeBlock("$ " + ToolPresenter.Str(input, "command"), "bash");
                case "Edit":
                    return new DiffView(ToolPresenter.Str(input, "old_string"), ToolPresenter.Str(input, "new_string"),
                        ToolPresenter.Relative(ToolPresenter.Str(input, "file_path")));
                case "MultiEdit":
                    return ToolPresenter.MultiEdit(input["edits"] as JArray);
                case "Write":
                    return ToolPresenter.WritePreview(input, ReadFile(ToolPresenter.Str(input, "file_path")));
                case CodexApprovals.ApplyPatchTool:
                    return ToolPresenter.PatchChanges(input["changes"] as JArray);
                case "WebFetch":
                {
                    string url = ToolPresenter.Str(input, "url") ?? string.Empty;
                    Label link = ParleyStyles.RichLabel("<link=\"" + url + "\"><color=" + ParleyStyles.LinkColor + ">" + InlineMarkdown.Escape(url) + "</color></link>",
                        "pl-request__detail");
                    link.tooltip = "Open " + url;
                    return link;
                }
                default:
                    return MarkdownView.CodeBlock(ToolPresenter.Truncate(input.ToString(Formatting.Indented), ToolPresenter.MaxOutputChars), "json");
            }
        }

        private static string ReadFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string resolved = ProjectPaths.Resolve(path);
            if (!File.Exists(resolved))
            {
                return null;
            }

            try
            {
                return File.ReadAllText(resolved);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Parley] Could not read " + resolved + ": " + exception.Message);
                return null;
            }
        }

        private static VisualElement ButtonRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-request__buttons");
            return row;
        }

        private static Label SelectableText(string text, string className)
        {
            Label label = ParleyStyles.Text(text, className);
            label.selection.isSelectable = true;
            return label;
        }

        private void Respond(Decision decision, string resolution)
        {
            _session.Respond(Block.RequestId, decision, resolution);
        }

        private void BuildResolved()
        {
            RemoveFromClassList("pl-request--question");
            RemoveFromClassList("pl-request--permission");
            AddToClassList("pl-request--resolved");
            Add(ParleyStyles.Text(ToolPresenter.DisplayName(Block.ToolName) + " — " + Block.Resolution, "pl-request__resolution"));
            string plan = ToolPresenter.Str(Block.Input, "plan");
            if (Block.ToolName != PendingRequest.ExitPlanModeTool || string.IsNullOrEmpty(plan))
            {
                return;
            }

            Foldout foldout = new Foldout { text = "Plan", value = false };
            MarkdownView view = new MarkdownView();
            view.SetMarkdown(plan);
            foldout.Add(view);
            Add(foldout);
        }

        private void BuildPermission(PendingRequest request)
        {
            string name = string.IsNullOrEmpty(request.DisplayName) ? ToolPresenter.DisplayName(request.ToolName) : request.DisplayName;
            Add(SelectableText(string.IsNullOrEmpty(request.Title) ? "Allow " + name + "?" : request.Title, "pl-request__title"));
            if (!string.IsNullOrEmpty(request.Description))
            {
                Add(SelectableText(request.Description, ParleyStyles.Muted));
            }

            Add(BuildPreview(request));
            if (!string.IsNullOrEmpty(request.DecisionReason))
            {
                Add(SelectableText("Why: " + request.DecisionReason, ParleyStyles.Muted));
            }

            if (!string.IsNullOrEmpty(request.BlockedPath))
            {
                Add(SelectableText("Path: " + request.BlockedPath, ParleyStyles.Muted));
            }

            VisualElement buttons = ButtonRow();
            Button allow = ParleyStyles.Button("Allow once", () => Respond(Decision.AllowWith(request.Input), "Allowed once"), "pl-button--primary");
            allow.tooltip = "Allow this call only; the agent asks again next time";
            buttons.Add(allow);
            AddAlwaysButton(buttons, request);
            Button deny = ParleyStyles.Button("Deny", () => Respond(Decision.Deny("The user denied this action."), "Denied"), "pl-button--danger");
            deny.tooltip = "Refuse this call; the agent continues without it";
            buttons.Add(deny);
            Add(buttons);
            Add(new FeedbackRow("Or tell the agent what to do instead…", "Deny with feedback", text => Respond(Decision.Deny(text), "Denied: " + text)));
        }

        private void AddAlwaysButton(VisualElement buttons, PendingRequest request)
        {
            string suggestions = DescribeSuggestions(request.Suggestions);
            bool codex = _session.Profile.Kind == ProfileKind.Codex;
            if (!codex && suggestions == null)
            {
                return;
            }

            string label = codex ? "Allow for this session" : "Always allow";
            string resolution = codex ? "Allowed for this session" : "Always allowed (" + suggestions + ")";
            Button always = ParleyStyles.Button(label, () => RespondAlways(request, resolution));
            always.tooltip = codex ? "Codex stops asking for matching calls in this chat (or saves the command rule it proposed)." : "Stop asking for: " + suggestions;
            buttons.Add(always);
        }

        private void RespondAlways(PendingRequest request, string resolution)
        {
            Decision decision = Decision.AllowWith(request.Input);
            decision.UpdatedPermissions = request.Suggestions;
            decision.Remember = _session.Profile.Kind != ProfileKind.ClaudeCode;
            Respond(decision, resolution);
        }

        private void BuildPlan(PendingRequest request)
        {
            Add(ParleyStyles.Text("Plan ready for review", "pl-request__title"));
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("pl-plan__scroll");
            MarkdownView view = new MarkdownView();
            string plan = ToolPresenter.Str(request.Input, "plan");
            if (string.IsNullOrEmpty(plan))
            {
                plan = ReadFile(ToolPresenter.Str(request.Input, "planFilePath")) ?? "(the plan text was not provided)";
            }

            view.SetMarkdown(plan);
            scroll.Add(view);
            Add(scroll);
            VisualElement buttons = ButtonRow();
            Button acceptEdits = ParleyStyles.Button("Approve and accept edits", () => ApprovePlan(request, PermissionMode.AcceptEdits, "Approved · auto-accept edits"),
                "pl-button--primary");
            acceptEdits.tooltip = "Start working; file edits apply without asking, commands still ask";
            buttons.Add(acceptEdits);
            Button review = ParleyStyles.Button("Approve and review edits", () => ApprovePlan(request, PermissionMode.Default, "Approved · review each edit"));
            review.tooltip = "Start working; the agent asks before each edit";
            buttons.Add(review);
            if (_session.Capabilities.Modes.Contains(PermissionMode.BypassPermissions))
            {
                Button bypass = ParleyStyles.Button("Approve and bypass permissions", () => ApprovePlanWithBypass(request), "pl-button--danger");
                bypass.tooltip = "Start working without asking for anything";
                buttons.Add(bypass);
            }

            Add(buttons);
            Add(new FeedbackRow("What should change in the plan?", "Keep planning", KeepPlanning, true));
        }

        private void ApprovePlan(PendingRequest request, PermissionMode next, string resolution)
        {
            Decision decision = Decision.AllowWith(request.Input);
            decision.NextMode = next;
            Respond(decision, resolution);
        }

        private void ApprovePlanWithBypass(PendingRequest request)
        {
            if (ParleyWindow.ConfirmBypass())
            {
                ApprovePlan(request, PermissionMode.BypassPermissions, "Approved · bypass permissions");
            }
        }

        private void KeepPlanning(string feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback))
            {
                Respond(Decision.Deny("Keep planning."), "Keep planning");
                return;
            }

            Respond(Decision.Deny(feedback), "Keep planning: " + feedback);
        }

        private void BuildEnterPlan(PendingRequest request)
        {
            Add(ParleyStyles.Text("Switch to plan mode?", "pl-request__title"));
            Add(ParleyStyles.Text("The agent wants to explore and present a plan before changing anything.", ParleyStyles.Muted));
            VisualElement buttons = ButtonRow();
            buttons.Add(ParleyStyles.Button("Enter plan mode", () => Respond(Decision.AllowWith(request.Input), "Entered plan mode"), "pl-button--primary"));
            buttons.Add(ParleyStyles.Button("No, continue", () => Respond(Decision.Deny("The user declined plan mode."), "Declined")));
            Add(buttons);
        }
    }
}