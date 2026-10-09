using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal static class ToolPaths
    {
        public static string Resolve(JObject input, string name = "file_path")
        {
            string path = ToolInput.String(input, name);
            return string.IsNullOrWhiteSpace(path) ? null : ProjectPaths.Resolve(path.Trim());
        }
    }
}