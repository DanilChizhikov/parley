using System.Text.RegularExpressions;

namespace DTech.Parley.Editor.Tools
{
    internal readonly struct GrepSearchRequest
    {
        public Regex Pattern { get; }
        public Regex Glob { get; }
        public string Root { get; }
        public string ProjectRoot { get; }
        public string Mode { get; }
        public int ContextLines { get; }
        public int Limit { get; }

        public GrepSearchRequest(
            Regex pattern,
            Regex glob,
            string root,
            string projectRoot,
            string mode,
            int contextLines,
            int limit)
        {
            Pattern = pattern;
            Glob = glob;
            Root = root;
            ProjectRoot = projectRoot;
            Mode = mode;
            ContextLines = contextLines;
            Limit = limit;
        }
    }
}
