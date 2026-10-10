using System.Collections.Generic;
using System.Globalization;
using DTech.Parley.Editor.Sessions;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class StatusBar : VisualElement
    {
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
            _account.style.whiteSpace = WhiteSpace.NoWrap;
            Add(_account);
            Add(ParleyStyles.Spacer());
            _usage = ParleyStyles.Text(string.Empty, "pl-status__usage");
            Add(_usage);
        }

        public void Refresh(ChatSession session)
        {
            bool waiting = session.OpenRequests.Count > 0;
            bool busy = session.IsBusy;
            _dot.EnableInClassList("pl-status__dot--busy", busy && !waiting);
            _dot.EnableInClassList("pl-status__dot--waiting", waiting);
            _dot.EnableInClassList("pl-status__dot--starting", session.IsStarting);
            _text.text = waiting ? "Waiting for you" : session.IsStarting ? "Starting…" : busy ? "Working…" : "Ready";
            _account.text = session.Capabilities.Account ?? string.Empty;
            _usage.text = Usage(session);
        }

        private static string FormatTokens(long tokens)
        {
            if (tokens < 1000)
            {
                return tokens.ToString(CultureInfo.InvariantCulture);
            }

            return (tokens / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";
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
                _usageParts.Add("ctx " + Mathf.RoundToInt(100.0f * session.ContextUsed / session.ContextMax) + "%");
            }

            return string.Join(" · ", _usageParts);
        }
    }
}