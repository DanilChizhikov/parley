using System;
using DTech.Parley.Editor.Sessions;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AgentsPanel : VisualElement
    {
        private const long RefreshIntervalMs = 250;

        private readonly AgentGraphView _graph = new ();
        private readonly AgentDetailsView _details = new ();

        private ChatSession _session;
        private AgentGraph _current;
        private string _selectedId = AgentNodeInfo.MainId;
        private string _renderedDetails;
        private bool _dirty = true;

        public int SubagentCount => _current?.SubagentCount ?? 0;

        public AgentsPanel(bool vertical, Action openWindow)
        {
            AddToClassList("pl-agents");
            if (openWindow != null)
            {
                VisualElement bar = new VisualElement();
                bar.AddToClassList("pl-agents__bar");
                bar.Add(ParleyStyles.Spacer());
                Button open = ParleyStyles.Button("Open in window", openWindow, "pl-button--small");
                open.tooltip = "Show the subagents graph in its own window";
                bar.Add(open);
                Add(bar);
            }

            TwoPaneSplitView split = new TwoPaneSplitView(1, vertical ? 220f : 360f,
                vertical ? TwoPaneSplitViewOrientation.Vertical : TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("pl-agents__split");
            split.Add(_graph);
            split.Add(_details);
            Add(split);
            _graph.OnAgentSelected += AgentSelectedHandler;
            schedule.Execute(Refresh).Every(RefreshIntervalMs);
        }

        public void Bind(ChatSession session)
        {
            if (_session == session)
            {
                return;
            }

            if (_session != null)
            {
                _session.OnEntryAdded -= EntryAddedHandler;
                _session.OnBlockAdded -= BlockAddedHandler;
                _session.OnBlockChanged -= BlockChangedHandler;
                _session.OnStateChanged -= StateChangedHandler;
            }

            _session = session;
            _current = null;
            _selectedId = AgentNodeInfo.MainId;
            _renderedDetails = null;
            _graph.ResetView();
            if (_session != null)
            {
                _session.OnEntryAdded += EntryAddedHandler;
                _session.OnBlockAdded += BlockAddedHandler;
                _session.OnBlockChanged += BlockChangedHandler;
                _session.OnStateChanged += StateChangedHandler;
            }

            _dirty = true;
            Refresh();
        }

        private void Refresh()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            AgentGraph graph = _session == null
                ? AgentGraph.Build(null, null, false)
                : AgentGraph.Build(_session.Record.Entries, _session.Tasks, _session.IsBusy);
            if (_current != null && graph.Signature == _current.Signature)
            {
                return;
            }

            _current = graph;
            if (graph.Find(_selectedId) == null)
            {
                _selectedId = AgentNodeInfo.MainId;
            }

            _graph.Show(graph, _selectedId);
            RenderDetails();
        }

        private void RenderDetails()
        {
            AgentNodeInfo info = _current?.Find(_selectedId);
            string signature = info == null ? null : info.Signature + "#" + _current.SubagentCount;
            if (signature == _renderedDetails)
            {
                return;
            }

            _renderedDetails = signature;
            _details.Show(info, _current?.SubagentCount ?? 0);
        }

        private void AgentSelectedHandler(string id)
        {
            _selectedId = id;
            RenderDetails();
        }

        private void EntryAddedHandler(TranscriptEntry entry)
        {
            _dirty = true;
        }

        private void BlockAddedHandler(TranscriptEntry entry, TranscriptBlock block)
        {
            _dirty = true;
        }

        private void BlockChangedHandler(TranscriptBlock block)
        {
            _dirty = true;
        }

        private void StateChangedHandler()
        {
            _dirty = true;
        }
    }
}
