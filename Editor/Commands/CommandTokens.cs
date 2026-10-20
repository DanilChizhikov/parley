using DTech.Parley.Editor.Panels;

namespace DTech.Parley.Editor.Commands
{
    internal static class CommandTokens
    {
        private const string PanelSpecToken = "${PANEL_SPEC}";

        public static string Expand(string body)
        {
            return string.IsNullOrEmpty(body) ? body : body.Replace(PanelSpecToken, PanelParser.Reference);
        }
    }
}
