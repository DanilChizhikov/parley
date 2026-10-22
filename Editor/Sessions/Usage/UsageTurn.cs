using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageTurn
	{
		[JsonProperty("index")]
		public int Index { get; set; }

		[JsonProperty("started")]
		public DateTime StartedUtc { get; set; }

		[JsonProperty("prompt")]
		public string Prompt { get; set; }

		[JsonProperty("durationMs")]
		public long DurationMs { get; set; }

		[JsonProperty("costUsd")]
		public double? CostUsd { get; set; }

		[JsonProperty("calls")]
		public List<UsageCall> Calls { get; set; } = new ();

		[JsonProperty("tools")]
		public List<UsageTool> Tools { get; set; } = new ();
	}
}
