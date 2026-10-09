namespace DTech.Parley.Editor
{
    internal static class PermissionModes
    {
        public static string ToWire(PermissionMode mode)
        {
            return mode switch
            {
                PermissionMode.AcceptEdits => "acceptEdits",
                PermissionMode.Plan => "plan",
                PermissionMode.Auto => "auto",
                PermissionMode.BypassPermissions => "bypassPermissions",
                PermissionMode.DontAsk => "dontAsk",
                _ => "default",
            };
        }

        public static PermissionMode FromWire(string value)
        {
            return value switch
            {
                "acceptEdits" => PermissionMode.AcceptEdits,
                "plan" => PermissionMode.Plan,
                "auto" => PermissionMode.Auto,
                "bypassPermissions" => PermissionMode.BypassPermissions,
                "dontAsk" => PermissionMode.DontAsk,
                _ => PermissionMode.Default,
            };
        }

        public static string DisplayName(PermissionMode mode)
        {
            return mode switch
            {
                PermissionMode.AcceptEdits => "Accept edits",
                PermissionMode.Plan => "Plan",
                PermissionMode.Auto => "Auto",
                PermissionMode.BypassPermissions => "Bypass permissions",
                PermissionMode.DontAsk => "Don't ask",
                _ => "Ask before edits",
            };
        }
    }
}