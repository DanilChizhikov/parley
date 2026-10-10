using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal sealed class UserTurn
    {
        public string Text = string.Empty;
        public List<ChatAttachment> Attachments = new ();
        public List<SkillInvocation> Skills = new ();
    }
}
