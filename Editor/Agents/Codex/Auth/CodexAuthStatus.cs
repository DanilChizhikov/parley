namespace DTech.Parley.Editor.Agents.Codex
{
    internal sealed class CodexAuthStatus
    {
        public bool LoggedIn { get; set; }
        public string Text { get; set; }
        public string Error { get; set; }

        public string Summary
        {
            get
            {
                if (!string.IsNullOrEmpty(Error))
                {
                    return Error;
                }

                if (!string.IsNullOrEmpty(Text))
                {
                    return Text;
                }

                return LoggedIn ? "Signed in" : "Not signed in";
            }
        }
    }
}