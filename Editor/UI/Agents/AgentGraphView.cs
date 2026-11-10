using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AgentGraphView : GraphView
    {
        public event Action<string> OnAgentSelected;

        private const float ColumnWidth = 300f;
        private const float RowHeight = 110f;
        private const Capabilities EdgeLockedCapabilities = Capabilities.Selectable | Capabilities.Deletable | Capabilities.Movable;

        private readonly Dictionary<string, AgentGraphNode> _nodes = new ();

        private string _structure;
        private bool _framed;

        public AgentGraphView()
        {
            AddToClassList("pl-agent-graph");
            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ClickSelector());
            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
            graphViewChanged = GraphViewChangedHandler;
            deleteSelection = DeleteSelectionHandler;
            RegisterCallback<GeometryChangedEvent>(_ => TryFrame());
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return new List<Port>();
        }

        public void Show(AgentGraph graph, string selectedId)
        {
            string structure = Structure(graph);
            if (structure == _structure)
            {
                foreach (AgentNodeInfo info in graph.Nodes)
                {
                    _nodes[info.Id].Apply(info);
                }

                return;
            }

            _structure = structure;
            Rebuild(graph);
            if (selectedId != null && _nodes.TryGetValue(selectedId, out AgentGraphNode selected))
            {
                AddToSelection(selected);
            }

            TryFrame();
        }

        public void ResetView()
        {
            _framed = false;
        }

        private static string Structure(AgentGraph graph)
        {
            List<string> parts = new List<string>(graph.Nodes.Count);
            foreach (AgentNodeInfo info in graph.Nodes)
            {
                parts.Add(info.Id + ">" + info.ParentId);
            }

            return string.Join("|", parts);
        }

        private static void LockEdge(Edge edge)
        {
            edge.capabilities &= ~EdgeLockedCapabilities;
            edge.pickingMode = PickingMode.Ignore;
            edge.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }

        private void TryFrame()
        {
            if (_framed || _nodes.Count == 0 || float.IsNaN(layout.width) || layout.width <= 0f)
            {
                return;
            }

            _framed = true;
            schedule.Execute(() => FrameAll());
        }

        private void Rebuild(AgentGraph graph)
        {
            foreach (GraphElement element in graphElements.ToList())
            {
                RemoveElement(element);
            }

            _nodes.Clear();
            foreach (AgentNodeInfo info in graph.Nodes)
            {
                AgentGraphNode node = new AgentGraphNode(info, NodeSelectedHandler);
                _nodes[info.Id] = node;
                AddElement(node);
            }

            float row = 0f;
            Place(graph.Main, ref row);
            foreach (AgentNodeInfo info in graph.Nodes)
            {
                if (info.IsMain || !_nodes.TryGetValue(info.ParentId, out AgentGraphNode parent))
                {
                    continue;
                }

                Edge edge = parent.Output.ConnectTo(_nodes[info.Id].Input);
                LockEdge(edge);
                AddElement(edge);
            }
        }
        private float Place(AgentNodeInfo info, ref float row)
        {
            float y;
            if (info.Children.Count == 0)
            {
                y = row * RowHeight;
                row++;
            }
            else
            {
                float first = 0f;
                float last = 0f;
                for (int i = 0; i < info.Children.Count; i++)
                {
                    float childY = Place(info.Children[i], ref row);
                    if (i == 0)
                    {
                        first = childY;
                    }

                    last = childY;
                }

                y = (first + last) * 0.5f;
            }

            if (_nodes.TryGetValue(info.Id, out AgentGraphNode node))
            {
                node.SetPosition(new Rect(info.Depth * ColumnWidth, y, 0f, 0f));
            }

            return y;
        }

        private static GraphViewChange GraphViewChangedHandler(GraphViewChange change)
        {
            change.elementsToRemove?.Clear();
            change.edgesToCreate?.Clear();
            change.movedElements?.Clear();
            return change;
        }

        private static void DeleteSelectionHandler(string operationName, AskUser askUser)
        {
        }

        private void NodeSelectedHandler(AgentGraphNode node)
        {
            OnAgentSelected?.Invoke(node.Id);
        }
    }
}
