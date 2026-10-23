using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace DTech.Parley.Editor.Sessions
{
	internal static class UsageXmlExporter
	{
		public const int FormatVersion = 1;

		public static XDocument Build(SessionRecord record, string model, DateTime exportedUtc)
		{
			UsageLog log = record.Usage ?? new UsageLog();
			UsageSummary summary = UsageSummary.Build(log, int.MaxValue);
			XElement root = new XElement("parleyUsage",
				new XAttribute("format", FormatVersion),
				new XAttribute("chat", record.Title ?? string.Empty),
				new XAttribute("profile", record.ProfileId ?? string.Empty),
				new XAttribute("backend", record.Kind.ToString()),
				new XAttribute("model", (string.IsNullOrEmpty(model) ? LastMainModel(log) : model) ?? string.Empty),
				new XAttribute("exported", Time(exportedUtc)));

			XElement total = Totals("summary", summary.Total);
			total.Add(new XAttribute("costUsd", Cost(summary.CostUsd)),
				new XAttribute("peakContext", summary.PeakContext),
				new XAttribute("toolResultChars", summary.ToolResultChars),
				new XAttribute("cacheRebuilds", summary.CacheRebuilds));
			root.Add(total);

			XElement models = new XElement("models");
			foreach (UsageGroup group in summary.Models)
			{
				models.Add(Totals("model", group.Totals, new XAttribute("name", group.Key)));
			}

			root.Add(models);
			XElement agents = new XElement("agents");
			foreach (UsageGroup group in summary.Agents)
			{
				agents.Add(Totals("agent", group.Totals, new XAttribute("id", group.Key), new XAttribute("label", group.Label)));
			}

			root.Add(agents);
			XElement turns = new XElement("turns");
			foreach (UsageTurn turn in log.Turns)
			{
				turns.Add(Turn(turn, summary));
			}

			root.Add(turns);
			return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
		}

		private static XElement Turn(UsageTurn turn, UsageSummary summary)
		{
			XElement element = Totals("turn", summary.TurnTotals(turn),
				new XAttribute("index", turn.Index),
				new XAttribute("started", Time(turn.StartedUtc)),
				new XAttribute("durationMs", turn.DurationMs));
			if (turn.CostUsd.HasValue)
			{
				element.Add(new XAttribute("costUsd", Cost(turn.CostUsd.Value)));
			}

			element.Add(new XElement("prompt", turn.Prompt ?? string.Empty));
			foreach (UsageCall call in turn.Calls)
			{
				XElement callElement = new XElement("call",
					new XAttribute("id", call.Id ?? string.Empty),
					new XAttribute("time", Time(call.TimeUtc)),
					new XAttribute("model", call.Model ?? string.Empty),
					new XAttribute("agent", call.Agent ?? UsageTracker.MainAgent),
					new XAttribute("input", call.Input),
					new XAttribute("cacheWrite", call.CacheWrite),
					new XAttribute("cacheRead", call.CacheRead),
					new XAttribute("output", call.Output),
					new XAttribute("reasoning", call.Reasoning),
					new XAttribute("thinkingChars", call.ThinkingChars));
				double? gap = summary.GapSeconds(call);
				if (gap.HasValue)
				{
					callElement.Add(new XAttribute("gapSec", gap.Value.ToString("0.0", CultureInfo.InvariantCulture)));
				}

				if (summary.IsCacheRebuild(call))
				{
					callElement.Add(new XAttribute("cacheRebuild", true));
				}

				element.Add(callElement);
			}

			foreach (UsageTool tool in turn.Tools)
			{
				element.Add(new XElement("tool",
					new XAttribute("id", tool.Id ?? string.Empty),
					new XAttribute("name", tool.Name ?? string.Empty),
					new XAttribute("agent", tool.Agent ?? UsageTracker.MainAgent),
					new XAttribute("call", tool.CallId ?? string.Empty),
					new XAttribute("target", tool.Target ?? string.Empty),
					new XAttribute("inputChars", tool.InputChars),
					new XAttribute("resultChars", tool.ResultChars),
					new XAttribute("error", tool.IsError)));
			}

			return element;
		}

		private static XElement Totals(string name, UsageTotals totals, params XAttribute[] leading)
		{
			return new XElement(name,
				leading,
				new XAttribute("calls", totals.Calls),
				new XAttribute("input", totals.Input),
				new XAttribute("cacheWrite", totals.CacheWrite),
				new XAttribute("cacheRead", totals.CacheRead),
				new XAttribute("output", totals.Output),
				new XAttribute("reasoning", totals.Reasoning));
		}

		private static string LastMainModel(UsageLog log)
		{
			for (int turnIndex = log.Turns.Count - 1; turnIndex >= 0; turnIndex--)
			{
				List<UsageCall> calls = log.Turns[turnIndex].Calls;
				for (int callIndex = calls.Count - 1; callIndex >= 0; callIndex--)
				{
					UsageCall call = calls[callIndex];
					if ((call.Agent == null || call.Agent == UsageTracker.MainAgent) && !string.IsNullOrEmpty(call.Model))
					{
						return call.Model;
					}
				}
			}

			return null;
		}

		private static string Time(DateTime utc)
		{
			return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
		}

		private static string Cost(double value)
		{
			return value.ToString("0.0000", CultureInfo.InvariantCulture);
		}
	}
}
