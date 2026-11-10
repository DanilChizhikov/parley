using System.Collections.Generic;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AgentNodeInfo
    {
        public const string MainId = "main";

        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string Prompt { get; set; }
        public string Result { get; set; }
        public AgentNodeStatus Status { get; set; }
        public int Depth { get; set; }
        public int ToolCount { get; set; }
        public string LastTool { get; set; }
        public string Signature { get; set; }
        public List<TranscriptBlock> Activity { get; } = new ();
        public List<AgentNodeInfo> Children { get; } = new ();

        public bool IsMain => Id == MainId;

        public string Title
        {
            get
            {
                if (IsMain)
                {
                    return "Main agent";
                }

                if (string.IsNullOrEmpty(Type))
                {
                    return string.IsNullOrEmpty(Description) ? "Subagent" : Description;
                }

                return string.IsNullOrEmpty(Description) ? Type : Type + ": " + Description;
            }
        }
    }
}
