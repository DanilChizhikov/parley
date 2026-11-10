using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AgentGraphNode : Node
    {
        private const float Width = 240f;

        private readonly Label _status;
        private readonly Label _detail;
        private readonly Action<AgentGraphNode> _selected;

        public string Id { get; }

        public Port Input { get; }

        public Port Output { get; }

        public AgentGraphNode(AgentNodeInfo info, Action<AgentGraphNode> selected)
        {
            Id = info.Id;
            _selected = selected;
            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable | Capabilities.Movable);
            style.width = Width;
            AddToClassList("pl-agent-node");
            if (!info.IsMain)
            {
                Input = CreatePort(Direction.Input);
                inputContainer.Add(Input);
            }

            Output = CreatePort(Direction.Output);
            outputContainer.Add(Output);
            VisualElement body = new VisualElement();
            body.AddToClassList("pl-agent-node__body");
            _status = ParleyStyles.Text(string.Empty, "pl-agent-node__status");
            body.Add(_status);
            _detail = ParleyStyles.Text(string.Empty, "pl-agent-node__detail");
            body.Add(_detail);
            extensionContainer.Add(body);
            Apply(info);
            RefreshExpandedState();
            RefreshPorts();
        }

        public static string Glyph(AgentNodeStatus status)
        {
            return status switch
            {
                AgentNodeStatus.Running => "●",
                AgentNodeStatus.Done => "✓",
                AgentNodeStatus.Failed => "✕",
                _ => "○",
            };
        }

        public void Apply(AgentNodeInfo info)
        {
            title = info.Title;
            tooltip = info.Title;
            _status.text = Glyph(info.Status) + " " + info.Status;
            foreach (AgentNodeStatus status in (AgentNodeStatus[])Enum.GetValues(typeof(AgentNodeStatus)))
            {
                EnableInClassList("pl-agent-node--" + status.ToString().ToLowerInvariant(), status == info.Status);
            }

            string tools = info.ToolCount == 1 ? "1 tool call" : info.ToolCount + " tool calls";
            _detail.text = string.IsNullOrEmpty(info.LastTool) ? tools : tools + " · last: " + ToolPresenter.DisplayName(info.LastTool);
        }

        public override void OnSelected()
        {
            base.OnSelected();
            _selected?.Invoke(this);
        }

        private Port CreatePort(Direction direction)
        {
            Port port = InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(AgentNodeInfo));
            port.portName = string.Empty;
            port.pickingMode = PickingMode.Ignore;
            if (port.edgeConnector != null)
            {
                port.RemoveManipulator(port.edgeConnector);
            }

            return port;
        }
    }
}
