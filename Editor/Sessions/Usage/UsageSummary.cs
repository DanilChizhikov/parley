using System.Collections.Generic;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageSummary
	{
		public const double CacheExpirySeconds = 300.0;

		private readonly Dictionary<UsageCall, double> _gaps = new ();
		private readonly HashSet<UsageCall> _rebuilds = new ();
		private readonly Dictionary<UsageTurn, UsageTotals> _turns = new ();

		public UsageTotals Total { get; } = new ();
		public List<UsageGroup> Models { get; } = new ();
		public List<UsageGroup> Agents { get; } = new ();
		public List<UsageTool> TopTools { get; } = new ();
		public double CostUsd { get; private set; }
		public long PeakContext { get; private set; }
		public long ToolResultChars { get; private set; }
		public int CacheRebuilds => _rebuilds.Count;

		public static UsageSummary Build(UsageLog log, int topTools)
		{
			UsageSummary summary = new UsageSummary();
			if (log != null)
			{
				summary.Collect(log, topTools);
			}

			return summary;
		}

		public double? GapSeconds(UsageCall call)
		{
			return _gaps.TryGetValue(call, out double gap) ? gap : (double?)null;
		}

		public bool IsCacheRebuild(UsageCall call)
		{
			return _rebuilds.Contains(call);
		}

		public UsageTotals TurnTotals(UsageTurn turn)
		{
			return _turns.TryGetValue(turn, out UsageTotals totals) ? totals : new UsageTotals();
		}

		private void Collect(UsageLog log, int topTools)
		{
			Dictionary<string, UsageCall> previous = new ();
			List<UsageTool> tools = new ();
			foreach (UsageTurn turn in log.Turns)
			{
				UsageTotals turnTotals = new UsageTotals();
				_turns[turn] = turnTotals;
				CostUsd += turn.CostUsd ?? 0;
				foreach (UsageCall call in turn.Calls)
				{
					Total.Add(call);
					turnTotals.Add(call);
					Group(Models, call.Model ?? "unknown", call.Model ?? "unknown").Totals.Add(call);
					string agent = call.Agent ?? UsageTracker.MainAgent;
					Group(Agents, agent, AgentLabel(log, agent)).Totals.Add(call);
					if (call.Context > PeakContext)
					{
						PeakContext = call.Context;
					}

					if (previous.TryGetValue(agent, out UsageCall last))
					{
						double gap = (call.TimeUtc - last.TimeUtc).TotalSeconds;
						_gaps[call] = gap;
						if (gap > CacheExpirySeconds && call.CacheWrite > call.CacheRead)
						{
							_rebuilds.Add(call);
						}
					}

					previous[agent] = call;
				}

				foreach (UsageTool tool in turn.Tools)
				{
					ToolResultChars += tool.ResultChars;
					tools.Add(tool);
				}
			}

			tools.Sort((left, right) => right.ResultChars.CompareTo(left.ResultChars));
			for (int i = 0; i < tools.Count && i < topTools; i++)
			{
				TopTools.Add(tools[i]);
			}
		}

		private static UsageGroup Group(List<UsageGroup> groups, string key, string label)
		{
			foreach (UsageGroup group in groups)
			{
				if (group.Key == key)
				{
					return group;
				}
			}

			UsageGroup added = new UsageGroup(key, label);
			groups.Add(added);
			return added;
		}

		private static string AgentLabel(UsageLog log, string agent)
		{
			if (agent == UsageTracker.MainAgent)
			{
				return UsageTracker.MainAgent;
			}

			return log.AgentLabels != null && log.AgentLabels.TryGetValue(agent, out string label) ? label : "subagent";
		}
	}
}
