using System.Collections.Generic;
using System.Globalization;
using DTech.Parley.Editor.Sessions;
using UnityEngine;
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
            _usage.EnableInClassList("pl-status__usage--warning", ContextPercent(session) >= ContextWarningPercent);
        }

        private static string FormatTokens(long tokens)
        {
            if (tokens < 1000)
            {
                return tokens.ToString(CultureInfo.InvariantCulture);
            }

            return (tokens / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        }

        private static int ContextPercent(ChatSession session)
        {
            return session.ContextMax > 0 ? Mathf.RoundToInt(100.0f * session.ContextUsed / session.ContextMax) : 0;
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
                lines.Add("Context window: " + FormatTokens(session.ContextUsed) + " of " + FormatTokens(session.ContextMax) + " used (" + ContextPercent(session) + "%)");
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

            if (session.ContextMax > 0)
            {
                _usageParts.Add("ctx " + ContextPercent(session) + "%");
            }

            return string.Join(" · ", _usageParts);
        }
    }
}