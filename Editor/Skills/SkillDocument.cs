using System.Collections.Generic;

namespace DTech.Parley.Editor.Skills
{
    internal sealed class SkillDocument
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ArgumentHint { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string ExtraFrontmatter { get; set; } = string.Empty;
        public string FolderPath { get; set; }
        public string Id => "skill:" + Name;
        public string PreferredProvider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string HandoffMode { get; set; } = "ask_approval";
        public List<string> AllowedTargets { get; set; } = new();
        public List<string> Diagnostics { get; } = new();
    }
}
