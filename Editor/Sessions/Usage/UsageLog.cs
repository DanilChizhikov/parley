using System.Collections.Generic;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageLog
	{
		[JsonProperty("turns")]
		public List<UsageTurn> Turns { get; set; } = new ();

		[JsonProperty("agents")]
		public Dictionary<string, string> AgentLabels { get; set; } = new ();
	}
}
