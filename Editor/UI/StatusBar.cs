using System;
using System.Collections.Generic;
using System.Globalization;
using DTech.Parley.Editor.Sessions;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class StatusBar : VisualElement
    {
        private const int ContextWarningPercent = 80;

        private readonly VisualElement _dot;
        private readonly Label _text;
        private readonly Label _account;
        private readonly Label _usage;
        private readonly Label _context;
        private readonly List<string> _usageParts = new ();

        public StatusBar()
        {
            AddToClassList("pl-status");
            _dot = new VisualElement();
            _dot.AddToClassList("pl-status__dot");
            Add(_dot);
            _text = ParleyStyles.Text("Ready", "pl-status__text");
            Add(_text);
            _account = ParleyStyles.Text(string.Empty, "pl-status__account");
            Add(_account);
            Add(ParleyStyles.Spacer());
            _usage = ParleyStyles.Text(string.Empty, "pl-status__usage");
            Add(_usage);
            _context = ParleyStyles.Text("Context: —", "pl-status__context");
            Add(_context);
        }

        public void Refresh(ChatSession session)
        {
            bool waiting = session.OpenRequests.Count > 0;
            bool busy = session.IsBusy;
            bool error = !string.IsNullOrEmpty(session.AuthMessage) && !busy && !session.IsStarting;
            _dot.EnableInClassList("pl-status__dot--busy", busy && !waiting);
            _dot.EnableInClassList("pl-status__dot--waiting", waiting);
            _dot.EnableInClassList("pl-status__dot--starting", session.IsStarting);
            _dot.EnableInClassList("pl-status__dot--error", error);
            _text.text = waiting ? "Waiting for you" : session.IsStarting ? "Starting…" : busy ? "Working…" : error ? "Needs attention" : "Ready";
            _account.text = session.Capabilities.Account ?? string.Empty;
            _account.tooltip = _account.text;
            _usage.text = Usage(session);
            _usage.tooltip = UsageTooltip(session);
            ParleyStyles.SetVisible(_usage, _usage.text.Length > 0);
            _context.text = ContextText(session);
            _context.tooltip = _usage.tooltip;
            _context.EnableInClassList("pl-status__context--warning", session.ContextMax > 0 &&
                100.0 * ContextUsed(session) / session.ContextMax >= ContextWarningPercent);
        }

        internal static string FormatTokens(long tokens)
        {
            if (tokens < 1000)
            {
                return tokens.ToString(CultureInfo.InvariantCulture);
            }

            if (tokens >= 1000000)
            {
                return (tokens / 1000000.0).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            }

            return (tokens / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        }

        private static int ContextPercent(ChatSession session)
        {
            return session.ContextMax > 0 ? (int)Math.Round(100.0 * ContextUsed(session) / session.ContextMax) : 0;
        }

        private static long ContextUsed(ChatSession session)
        {
            return Math.Max(0, Math.Min(session.ContextUsed, session.ContextMax));
        }

        private static string ContextText(ChatSession session)
        {
            if (session.ContextMax <= 0)
            {
                return "Context: —";
            }

            long remaining = session.ContextMax - ContextUsed(session);
            int percent = (int)Math.Floor(100.0 * remaining / session.ContextMax);
            string estimate = session.Provider == ProfileKind.Local ? "~" : string.Empty;
            return "Context: " + estimate + percent + "% left · " + estimate + FormatTokens(remaining) +
                " / " + FormatTokens(session.ContextMax);
        }

        private static string UsageTooltip(ChatSession session)
        {
            List<string> lines = new List<string>();
            TurnResult turn = session.LastTurn;
            if (turn != null && (turn.InputTokens > 0 || turn.OutputTokens > 0))
            {
                long input = turn.InputTokens + turn.CacheReadTokens + turn.CacheCreationTokens;
                lines.Add("Last turn: " + FormatTokens(input) + " tokens in (" + FormatTokens(turn.CacheReadTokens) + " from cache), " + FormatTokens(turn.OutputTokens) + " out");
            }

            if (session.Record.CostUsd > 0)
            {
                lines.Add("Chat cost so far: $" + session.Record.CostUsd.ToString("0.000", CultureInfo.InvariantCulture));
            }

            if (session.ContextMax > 0)
            {
                long used = ContextUsed(session);
                lines.Add("Context window: " + used.ToString(CultureInfo.InvariantCulture) + " of " +
                    session.ContextMax.ToString(CultureInfo.InvariantCulture) + " tokens used (" +
                    ContextPercent(session) + "%). " +
                    (session.ContextMax - used).ToString(CultureInfo.InvariantCulture) + " tokens remaining.");
                lines.Add(session.Provider == ProfileKind.Local
                    ? "Source: estimated local context usage and the configured context window."
                    : "Source: latest context usage reported by this chat's backend.");
            }
            else
            {
                lines.Add("Context usage is not available yet. Waiting for this chat's backend to report it.");
            }

            long characters = ContextCharacters(session);
            lines.Add("Text estimate: " + characters + " characters, ~" + FormatTokens((characters + 3) / 4) +
                " tokens (4 characters/token; excludes tools, images and provider instructions).");
            if (session.Record.ContextSnapshot != null)
            {
                lines.Add("Handoff: " + session.Record.ContextSnapshot.Characters + " characters; " +
                    (session.Record.ContextDelivered ? "sent to agent" : "included with your next message"));
            }

            return string.Join("\n", lines);
        }

        private string Usage(ChatSession session)
        {
            _usageParts.Clear();
            TurnResult turn = session.LastTurn;
            if (turn != null && (turn.InputTokens > 0 || turn.OutputTokens > 0))
            {
                long input = turn.InputTokens + turn.CacheReadTokens + turn.CacheCreationTokens;
                _usageParts.Add("↑" + FormatTokens(input) + " ↓" + FormatTokens(turn.OutputTokens));
            }

            if (session.Record.CostUsd > 0)
            {
                _usageParts.Add("$" + session.Record.CostUsd.ToString("0.000", CultureInfo.InvariantCulture));
            }

            return string.Join(" · ", _usageParts);
        }

        internal static long ContextCharacters(ChatSession session)
        {
            long characters = session.Record.ContextSnapshot?.Characters ?? 0;
            foreach (TranscriptEntry entry in session.Messages)
            {
                if (entry.Role != EntryRole.User && entry.Role != EntryRole.Assistant)
                {
                    continue;
                }
                foreach (TranscriptBlock block in entry.Blocks)
                {
                    if (block.Kind == BlockKind.Text && string.IsNullOrEmpty(block.ParentToolUseId))
                    {
                        characters += block.Text?.Length ?? 0;
                    }
                }
            }

            return characters;
        }
    }
}
