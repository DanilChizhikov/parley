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
    }
}
