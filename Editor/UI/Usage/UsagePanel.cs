using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using DTech.Parley.Editor.Sessions;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class UsagePanel : VisualElement
    {
        private const int TopToolCount = 10;
        private const int PromptPreviewLength = 80;

        private readonly VisualElement _body;
        private readonly Button _copy;
        private readonly Button _export;

        private ChatSession _session;
        private string _renderedKey;

        public UsagePanel()
        {
            AddToClassList("pl-usage");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-usage__header");
            header.Add(ParleyStyles.Text("Token usage", "pl-usage__title"));
            header.Add(ParleyStyles.Spacer());
            _copy = ParleyStyles.Button("Copy XML", CopyXml, "pl-button--small");
            _copy.tooltip = "Copy the usage report as XML to paste into a chat";
            header.Add(_copy);
            _export = ParleyStyles.Button("Export XML…", ExportXml, "pl-button--small");
            _export.tooltip = "Save the usage report as an XML file";
            header.Add(_export);
            Add(header);
            _body = new VisualElement();
            Add(_body);
        }

        public static long TotalTokens(ChatSession session)
        {
            long total = 0;
            if (session?.Record.Usage == null)
            {
                return total;
            }

            foreach (UsageTurn turn in session.Record.Usage.Turns)
            {
                foreach (UsageCall call in turn.Calls)
                {
                    total += call.Context + call.Output;
                }
            }

            return total;
        }

        public void Bind(ChatSession session)
        {
            _session = session;
            _renderedKey = null;
        }

        public void Refresh()
        {
            UsageLog log = _session?.Record.Usage;
            string key = RenderKey(log);
            if (key == _renderedKey)
            {
                return;
            }

            _renderedKey = key;
            _body.Clear();
            UsageSummary summary = UsageSummary.Build(log, TopToolCount);
            bool empty = summary.Total.Calls == 0;
            _copy.SetEnabled(!empty);
            _export.SetEnabled(!empty);
            if (empty)
            {
                _body.Add(ParleyStyles.Text("No usage yet. It is recorded for turns sent after this update.", ParleyStyles.Muted));
                return;
            }

            AddTotals(summary);
            Section("By model");
            foreach (UsageGroup group in summary.Models)
            {
                Row(group.Label, Describe(group.Totals));
            }

            Section("By agent");
            foreach (UsageGroup group in summary.Agents)
            {
                Row(group.Label, Describe(group.Totals));
            }

            Section("Turns");
            for (int i = log.Turns.Count - 1; i >= 0; i--)
            {
                AddTurn(log.Turns[i], summary);
            }

            if (summary.TopTools.Count > 0)
            {
                Section("Largest tool results");
                foreach (UsageTool tool in summary.TopTools)
                {
                    Row(tool.Name + (string.IsNullOrEmpty(tool.Target) ? string.Empty : " " + tool.Target),
                        tool.ResultChars.ToString("N0", CultureInfo.InvariantCulture) + " chars" + (tool.Agent == UsageTracker.MainAgent ? string.Empty : " · subagent"));
                }
            }
        }

        private void AddTotals(UsageSummary summary)
        {
            Section("Total");
            Row(summary.Total.Calls + " API calls", Describe(summary.Total));
            StringBuilder details = new StringBuilder();
            details.Append("Peak context ").Append(StatusBar.FormatTokens(summary.PeakContext));
            if (summary.CostUsd > 0)
            {
                details.Append(" · $").Append(summary.CostUsd.ToString("0.000", CultureInfo.InvariantCulture));
            }

            details.Append(" · tool results ").Append(StatusBar.FormatTokens(summary.ToolResultChars)).Append(" chars");
            Row("Context and cost", details.ToString());
            if (summary.CacheRebuilds > 0)
            {
                Label warning = ParleyStyles.Text("Prompt cache expired " + summary.CacheRebuilds +
                    " time(s) after a pause: the whole context was written again. Start a new chat after long breaks.", "pl-usage__warning");
                _body.Add(warning);
            }
        }

        private void AddTurn(UsageTurn turn, UsageSummary summary)
        {
            UsageTotals totals = summary.TurnTotals(turn);
            string title = "#" + turn.Index + " " + Preview(turn.Prompt);
            StringBuilder value = new StringBuilder(Describe(totals));
            if (turn.CostUsd.HasValue)
            {
                value.Append(" · $").Append(turn.CostUsd.Value.ToString("0.000", CultureInfo.InvariantCulture));
            }

            foreach (UsageCall call in turn.Calls)
            {
                if (summary.IsCacheRebuild(call))
                {
                    value.Append(" · cache rebuilt");
                    break;
                }
            }

            Row(title, value.ToString());
        }

        private void Section(string title)
        {
            _body.Add(ParleyStyles.Text(title, "pl-usage__section"));
        }

        private void Row(string title, string value)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-usage__row");
            Label name = ParleyStyles.Text(title, "pl-usage__name");
            name.tooltip = title;
            row.Add(name);
            row.Add(ParleyStyles.Text(value, ParleyStyles.Muted));
            _body.Add(row);
        }

        private void CopyXml()
        {
            XDocument document = BuildDocument();
            if (document != null)
            {
                EditorGUIUtility.systemCopyBuffer = document.ToString();
            }
        }

        private void ExportXml()
        {
            XDocument document = BuildDocument();
            if (document == null)
            {
                return;
            }

            string name = "parley-usage-" + SafeName(_session.Record.Title) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            string path = EditorUtility.SaveFilePanel("Export token usage", string.Empty, name, "xml");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, document.Declaration + Environment.NewLine + document, new UTF8Encoding(false));
            EditorUtility.RevealInFinder(path);
        }

        private XDocument BuildDocument()
        {
            return _session == null ? null : UsageXmlExporter.Build(_session.Record, _session.Model, DateTime.UtcNow);
        }

        private static string Describe(UsageTotals totals)
        {
            return "in " + StatusBar.FormatTokens(totals.Input) +
                " · cache write " + StatusBar.FormatTokens(totals.CacheWrite) +
                " · cache read " + StatusBar.FormatTokens(totals.CacheRead) +
                " · out " + StatusBar.FormatTokens(totals.Output) +
                (totals.Reasoning > 0 ? " · reasoning " + StatusBar.FormatTokens(totals.Reasoning) : string.Empty) +
                " · " + totals.Calls + " calls";
        }

        private static string Preview(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return "(no prompt)";
            }

            return prompt.Length <= PromptPreviewLength ? prompt : prompt.Substring(0, PromptPreviewLength - 1) + "…";
        }

        private static string SafeName(string title)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char character in title ?? string.Empty)
            {
                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
                if (builder.Length >= 40)
                {
                    break;
                }
            }

            string name = builder.ToString().Trim('-');
            return name.Length == 0 ? "chat" : name;
        }

        private static string RenderKey(UsageLog log)
        {
            if (log == null)
            {
                return string.Empty;
            }

            long checksum = 0;
            int calls = 0;
            int tools = 0;
            foreach (UsageTurn turn in log.Turns)
            {
                calls += turn.Calls.Count;
                tools += turn.Tools.Count;
                checksum += (long)(turn.CostUsd.GetValueOrDefault() * 10000);
                foreach (UsageCall call in turn.Calls)
                {
                    checksum += call.Context + call.Output;
                }

                foreach (UsageTool tool in turn.Tools)
                {
                    checksum += tool.ResultChars;
                }
            }

            return log.Turns.Count + "|" + calls + "|" + tools + "|" + checksum;
        }
    }
}
