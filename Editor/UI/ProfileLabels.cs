namespace DTech.Parley.Editor.UI
{
    internal static class ProfileLabels
    {
        public static string Describe(ParleyProfile profile)
        {
            return profile.Kind switch
            {
                ProfileKind.ClaudeCode => "Claude Code",
                ProfileKind.Codex => "Codex",
                _ => LocalPresets.DisplayName(profile.Preset),
            };
        }
    }
}