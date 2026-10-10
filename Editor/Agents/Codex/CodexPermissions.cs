using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexPermissions
    {
        public const string PlanCollaboration = "plan";
        public const string DefaultCollaboration = "default";

        public static readonly PermissionMode[] Supported =
        {
            PermissionMode.Default,
            PermissionMode.AcceptEdits,
            PermissionMode.Plan,
            PermissionMode.DontAsk,
            PermissionMode.BypassPermissions,
        };

        public static PermissionMode Normalize(PermissionMode mode)
        {
            return mode == PermissionMode.Auto ? PermissionMode.Default : mode;
        }

        public static string ApprovalPolicy(PermissionMode mode)
        {
            return mode switch
            {
                PermissionMode.DontAsk => "never",
                PermissionMode.BypassPermissions => "never",
                _ => "on-request",
            };
        }

        public static string SandboxMode(PermissionMode mode)
        {
            return mode switch
            {
                PermissionMode.AcceptEdits => "workspace-write",
                PermissionMode.BypassPermissions => "danger-full-access",
                _ => "read-only",
            };
        }

        public static JObject SandboxPolicy(PermissionMode mode, IEnumerable<string> additionalDirectories)
        {
            switch (mode)
            {
                case PermissionMode.AcceptEdits:
                    JArray writableRoots = new JArray();
                    foreach (string directory in additionalDirectories)
                    {
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            writableRoots.Add(ProjectPaths.Resolve(directory));
                        }
                    }

                    return new JObject { ["type"] = "workspaceWrite", ["writableRoots"] = writableRoots, ["networkAccess"] = false };
                case PermissionMode.BypassPermissions:
                    return new JObject { ["type"] = "dangerFullAccess" };
                default:
                    return new JObject { ["type"] = "readOnly" };
            }
        }

        public static string CollaborationMode(PermissionMode mode)
        {
            return mode == PermissionMode.Plan ? PlanCollaboration : DefaultCollaboration;
        }
    }
}