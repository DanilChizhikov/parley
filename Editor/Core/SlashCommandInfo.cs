using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal sealed class SlashCommandInfo
    {
        public List<string> Aliases { get; } = new ();
        public string Name { get; set; }
        public string Description { get; set; }
        public string ArgumentHint { get; set; }
        public bool IsBuiltin { get; set; }
    }
}
