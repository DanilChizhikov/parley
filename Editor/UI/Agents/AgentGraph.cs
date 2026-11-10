using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AgentGraph
    {
        private static readonly string[] _subagentTools = { "Agent", "Task" };

        private readonly Dictionary<string, AgentNodeInfo> _byId = new ();
        private readonly List<AgentNodeInfo> _nodes = new ();

        public AgentNodeInfo Main { get; }

        // Depth-first order, main agent first: parents always come before their children.
        public IReadOnlyList<AgentNodeInfo> Nodes => _nodes;

        public int SubagentCount => _nodes.Count - 1;

        public string Signature { get; private set; }

        private AgentGraph(AgentNodeInfo main)
        {
            Main = main;
        }

        public static bool IsSubagentTool(string toolName)
        {
            return Array.IndexOf(_subagentTools, toolName) >= 0;
        }

        public static AgentGraph Build(IReadOnlyList<TranscriptEntry> entries, IEnumerable<BackgroundTaskInfo> tasks, bool mainRunning)
        {
            AgentNodeInfo main = new AgentNodeInfo
            {
                Id = AgentNodeInfo.MainId,
                Status = mainRunning ? AgentNodeStatus.Running : AgentNodeStatus.Done,
            };

            Dictionary<string, AgentNodeInfo> byId = new () { [main.Id] = main };
            List<AgentNodeInfo> created = new ();
            Dictionary<string, BackgroundTaskInfo> tasksByToolUse = IndexTasks(tasks);
            if (entries != null)
            {
                foreach (TranscriptEntry entry in entries)
                {
                    foreach (TranscriptBlock block in entry.Blocks)
                    {
                        if (block.Kind != BlockKind.ToolUse || !IsSubagentTool(block.ToolName) || string.IsNullOrEmpty(block.ToolUseId)
                            || byId.ContainsKey(block.ToolUseId))
                        {
                            continue;
                        }

                        tasksByToolUse.TryGetValue(block.ToolUseId, out BackgroundTaskInfo task);
                        AgentNodeInfo node = CreateNode(block, task);
                        byId[node.Id] = node;
                        created.Add(node);
                    }
                }

                foreach (TranscriptEntry entry in entries)
                {
                    if (entry.Role != EntryRole.Assistant)
                    {
                        continue;
                    }

                    foreach (TranscriptBlock block in entry.Blocks)
                    {
                        AddActivity(byId, block);
                    }
                }
            }

            foreach (AgentNodeInfo node in created)
            {
                AgentNodeInfo parent = node.ParentId != null && node.ParentId != node.Id && byId.TryGetValue(node.ParentId, out AgentNodeInfo found)
                    ? found
                    : main;
                node.ParentId = parent.Id;
                parent.Children.Add(node);
            }

            AgentGraph graph = new AgentGraph(main);
            StringBuilder signature = new StringBuilder();
            graph.Collect(main, 0, signature);
            graph.Signature = signature.ToString();
            return graph;
        }

        public AgentNodeInfo Find(string id)
        {
            return id != null && _byId.TryGetValue(id, out AgentNodeInfo node) ? node : null;
        }

        private static Dictionary<string, BackgroundTaskInfo> IndexTasks(IEnumerable<BackgroundTaskInfo> tasks)
        {
            Dictionary<string, BackgroundTaskInfo> index = new ();
            if (tasks == null)
            {
                return index;
            }

            foreach (BackgroundTaskInfo task in tasks)
            {
                if (!string.IsNullOrEmpty(task.ToolUseId))
                {
                    index[task.ToolUseId] = task;
                }
            }

            return index;
        }

        private static AgentNodeInfo CreateNode(TranscriptBlock block, BackgroundTaskInfo task)
        {
            JObject input = block.Input;
            return new AgentNodeInfo
            {
                Id = block.ToolUseId,
                ParentId = string.IsNullOrEmpty(block.ParentToolUseId) ? AgentNodeInfo.MainId : block.ParentToolUseId,
                Type = (string)input?["subagent_type"],
                Description = (string)input?["description"] ?? task?.Description,
                Prompt = (string)input?["prompt"],
                Result = string.IsNullOrEmpty(task?.Summary) ? block.Result : task.Summary,
                Status = StatusOf(block, task),
            };
        }

        // A background agent answers its tool call right away, so its task status is the real one.
        private static AgentNodeStatus StatusOf(TranscriptBlock block, BackgroundTaskInfo task)
        {
            if (block.IsError)
            {
                return AgentNodeStatus.Failed;
            }

            if (task != null)
            {
                return task.Status switch
                {
                    "running" or "pending" => AgentNodeStatus.Running,
                    "completed" => AgentNodeStatus.Done,
                    "failed" or "error" => AgentNodeStatus.Failed,
                    _ => AgentNodeStatus.Stopped,
                };
            }

            if (block.Result != null)
            {
                return AgentNodeStatus.Done;
            }

            return block.IsFinished ? AgentNodeStatus.Stopped : AgentNodeStatus.Running;
        }

        private static void AddActivity(Dictionary<string, AgentNodeInfo> byId, TranscriptBlock block)
        {
            if (block.Kind != BlockKind.Text && block.Kind != BlockKind.ToolUse)
            {
                return;
            }

            string owner = string.IsNullOrEmpty(block.ParentToolUseId) ? AgentNodeInfo.MainId : block.ParentToolUseId;
            if (!byId.TryGetValue(owner, out AgentNodeInfo node))
            {
                node = byId[AgentNodeInfo.MainId];
            }

            node.Activity.Add(block);
            if (block.Kind == BlockKind.ToolUse)
            {
                node.ToolCount++;
                node.LastTool = block.ToolName;
            }
        }

        private void Collect(AgentNodeInfo node, int depth, StringBuilder signature)
        {
            if (_byId.ContainsKey(node.Id))
            {
                return;
            }

            node.Depth = depth;
            _nodes.Add(node);
            _byId[node.Id] = node;
            node.Signature = node.Id + ">" + node.ParentId + ":" + (int)node.Status + ":" + node.ToolCount + ":" + node.Activity.Count + ":"
                + TextLength(node) + ":" + node.Children.Count;
            signature.Append(node.Signature).Append('|');
            foreach (AgentNodeInfo child in node.Children)
            {
                Collect(child, depth + 1, signature);
            }
        }

        private static int TextLength(AgentNodeInfo node)
        {
            int length = node.Result?.Length ?? 0;
            if (node.Activity.Count > 0)
            {
                length += node.Activity[node.Activity.Count - 1].Text?.Length ?? 0;
            }

            return length;
        }
    }
}
