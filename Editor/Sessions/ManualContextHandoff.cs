using System;

namespace DTech.Parley.Editor.Sessions
{
    internal static class ManualContextHandoff
    {
        public static ChatSession FindParent(IChatTabManager tabs, ChatSession child)
        {
            foreach (ChatSession tab in tabs.Tabs)
            {
                if (tab != child && tab.SessionId == child.Record.ParentSessionId)
                {
                    return tab;
                }
            }

            return null;
        }

        public static ChatSession Prepare(IChatTabManager tabs, ChatSession source, ChatSession target,
            ParleyProfile newProfile, ContextSnapshot snapshot, bool returningResult)
        {
            if (!Contains(tabs, source))
            {
                throw new InvalidOperationException("The source tab was closed. Nothing was transferred.");
            }

            if (returningResult && (target == null || FindParent(tabs, source) != target))
            {
                throw new InvalidOperationException("The parent tab is closed. Open it from History first.");
            }

            if (target != null)
            {
                if (!Contains(tabs, target) || target == source)
                {
                    throw new InvalidOperationException("Choose another open tab.");
                }

                if (target.HasActiveWork)
                {
                    throw new InvalidOperationException("The target tab is busy. Wait or choose another tab.");
                }

                string message = returningResult ? "# Result from child tab\n\n" + snapshot.Markdown : snapshot.Markdown;
                target.Record.Draft = string.IsNullOrWhiteSpace(target.Record.Draft)
                    ? message : target.Record.Draft + "\n\n" + message;
                tabs.Select(target);
            }
            else
            {
                target = tabs.Open(newProfile, new SessionRecord
                {
                    ProfileId = newProfile.Id,
                    Kind = newProfile.Kind,
                    ParentSessionId = snapshot.SourceSessionId,
                    Title = "Context handoff",
                    Draft = snapshot.Markdown,
                });
            }

            tabs.Save();
            return target;
        }

        private static bool Contains(IChatTabManager tabs, ChatSession session)
        {
            foreach (ChatSession tab in tabs.Tabs)
            {
                if (tab == session)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
