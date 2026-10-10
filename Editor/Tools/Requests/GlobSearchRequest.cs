using System.Text.RegularExpressions;

namespace DTech.Parley.Editor.Tools
{
    internal readonly struct GlobSearchRequest
    {
        public Regex Pattern { get; }
        public string Root { get; }
        public string ProjectRoot { get; }
        public bool IncludeMeta { get; }

        public GlobSearchRequest(
            Regex pattern,
            string root,
            string projectRoot,
            bool includeMeta)
        {
            Pattern = pattern;
            Root = root;
            ProjectRoot = projectRoot;
            IncludeMeta = includeMeta;
        }
    }
}
