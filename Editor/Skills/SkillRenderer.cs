namespace DTech.Parley.Editor.Skills
{
    internal static class SkillRenderer
    {
        private const string ArgumentsToken = "$ARGUMENTS";
        private const string DirectoryToken = "${CLAUDE_SKILL_DIR}";

        public static string Render(string body, string directory, string arguments)
        {
            string text = (body ?? string.Empty).Replace(DirectoryToken, directory ?? string.Empty);
            string value = (arguments ?? string.Empty).Trim();
            if (text.Contains(ArgumentsToken))
            {
                return text.Replace(ArgumentsToken, value);
            }

            return value.Length == 0 ? text : text.TrimEnd() + "\n\nARGUMENTS: " + value;
        }
    }
}
