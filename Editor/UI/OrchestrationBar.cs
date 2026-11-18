using System.Linq;
using DTech.Parley.Editor.Orchestration;
using DTech.Parley.Editor.Sessions;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class OrchestrationBar : VisualElement
    {
        private readonly IChatTabManager _tabs;
        private bool _dirty = true;
        private ChatSession _active;

        public VisualElement TargetApproval { get; } = new();

        public OrchestrationBar(IChatTabManager tabs)
        {
            _tabs = tabs;
            AddToClassList("pl-orchestration");
            TargetApproval.AddToClassList("pl-orchestration-approval");
            Subscribe();
            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_tabs.Engine != null) _tabs.Engine.OnChanged -= ChangedHandler;
            });
            Refresh();
        }

        public void Refresh()
        {
            if (!_dirty && _active == _tabs.Active) return;
            _dirty = false;
            _active = _tabs.Active;
            Clear();
            TargetApproval.Clear();
            if (_tabs.Engine != null)
            {
                foreach (OrchestrationRun run in _tabs.Engine.Runs.Where(run => run.Delegations.Count > 0 &&
                    (run.Status == RunStatus.Running || (run.Status == RunStatus.Interrupted &&
                    (run.Steps.Any(step => step.SessionId == _active?.SessionId) ||
                    run.Delegations.Any(request => request.ChildSessionId == _active?.SessionId))))))
                {
                    AddRun(run);
                }
            }
            ParleyStyles.SetVisible(this, childCount > 0);
            ParleyStyles.SetVisible(TargetApproval, TargetApproval.childCount > 0);
        }

        private void Subscribe()
        {
            if (_tabs.Engine == null) return;
            _tabs.Engine.OnChanged -= ChangedHandler;
            _tabs.Engine.OnChanged += ChangedHandler;
            _dirty = true;
        }

        private void AddRun(OrchestrationRun run)
        {
            VisualElement row = new();
            row.AddToClassList("pl-orchestration__row");
            int completed = run.Delegations.Count(request => request.Status == DelegationStatus.Delivered);
            row.Add(new Label(run.Status == RunStatus.Interrupted ? "Chain interrupted · nothing replayed" :
                "Chain · " + completed + "/" + run.Delegations.Count + " delivered"));
            ScrollView nodes = new(ScrollViewMode.Horizontal);
            nodes.AddToClassList("pl-orchestration__nodes");
            nodes.contentContainer.style.flexDirection = FlexDirection.Row;
            nodes.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            row.Add(nodes);
            AddNode(nodes, run.RootSessionId, "Root", run.Status.ToString(), null);
            foreach (DelegationRecord request in run.Delegations)
            {
                nodes.Add(new Label(" ➜ "));
                AddNode(nodes, request.ChildSessionId, request.TargetSkill, Status(request.Status),
                    () => _tabs.Engine.OpenApprovalTarget(run.Id, request.Id));
                if (run.Status != RunStatus.Running) continue;
                if (request.Status == DelegationStatus.WaitingApproval ||
                    request.Status == DelegationStatus.WaitingResultApproval)
                {
                    AddApproval(nodes, run, request);
                    if (_active?.SessionId == request.ChildSessionId) AddApproval(TargetApproval, run, request);
                }
            }
            if (run.Status == RunStatus.Running)
                row.Add(ParleyStyles.Button("Stop Chain", () => _tabs.Engine.StopChain(run.Id), "pl-button--danger"));
            Add(row);
        }

        private void AddNode(VisualElement parent, string sessionId, string fallback, string status,
            System.Func<ChatSession> open)
        {
            ChatSession session = _tabs.Tabs.FirstOrDefault(item => item.SessionId == sessionId);
            string title = session == null ? fallback : session.Title;
            if (session != null && !string.IsNullOrEmpty(session.Record.RoleSkillId))
                title += " (" + session.Record.RoleSkillId.Replace("skill:", string.Empty) + ")";
            Button node = ParleyStyles.Button(title + " [" + status + "]", () =>
            {
                ChatSession target = session ?? open?.Invoke();
                if (target != null) _tabs.Select(target);
                _dirty = true;
                Refresh();
            });
            node.SetEnabled(session != null || (open != null && status == "Awaiting approval"));
            node.EnableInClassList("pl-chat-tab--active", session != null && session == _tabs.Active);
            parent.Add(node);
        }

        private void AddApproval(VisualElement parent, OrchestrationRun run, DelegationRecord request)
        {
            bool result = request.Status == DelegationStatus.WaitingResultApproval;
            DelegationStatus expectedStatus = request.Status;
            VisualElement gate = new();
            gate.AddToClassList("pl-orchestration__gate");
            gate.Add(new Label(result ? "Review child result before returning to parent" :
                "Approve " + request.TargetSkill)
                { tooltip = request.TaskSummary });
            gate.Add(ParleyStyles.Button(result ? "Approve Result" : "Approve & Run",
                () => _tabs.Engine.Approve(run.Id, request.Id, true, expectedStatus)));
            gate.Add(ParleyStyles.Button(result ? "Withhold Result" : "Deny",
                () => _tabs.Engine.Approve(run.Id, request.Id, false, expectedStatus)));
            parent.Add(gate);
        }

        private static string Status(DelegationStatus status) => status switch
        {
            DelegationStatus.WaitingApproval => "Awaiting approval",
            DelegationStatus.WaitingResultApproval => "Review result",
            DelegationStatus.Running => "Working…",
            DelegationStatus.Preparing => "Preparing…",
            _ => status.ToString(),
        };

        private void ChangedHandler() { _dirty = true; }
    }
}
