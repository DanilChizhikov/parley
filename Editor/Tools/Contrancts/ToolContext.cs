using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Parley.Editor.Tools
{
    internal sealed class ToolContext
    {
        public HashSet<string> ReadFiles { get; } = new (StringComparer.OrdinalIgnoreCase);
        public string ProjectRoot { get; set; } = ProjectPaths.Root;
        public IReadOnlyList<string> AdditionalDirectories { get; set; } = Array.Empty<string>();
        public Func<PermissionMode> GetMode { get; set; } = () => PermissionMode.Default;
        public Action<PermissionMode> SetMode { get; set; } = _ => { };
        public Func<PendingRequest, CancellationToken, Task<Decision>> AskUser { get; set; }
        public string ToolUseId { get; set; }
    }
}