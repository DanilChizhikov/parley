using System;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageCall
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("time")]
		public DateTime TimeUtc { get; set; }

		[JsonProperty("model")]
		public string Model { get; set; }

		[JsonProperty("agent")]
		public string Agent { get; set; }

		[JsonProperty("input")]
		public long Input { get; set; }

		[JsonProperty("cacheWrite")]
		public long CacheWrite { get; set; }

		[JsonProperty("cacheRead")]
		public long CacheRead { get; set; }

		[JsonProperty("output")]
		public long Output { get; set; }

		[JsonProperty("reasoning")]
		public long Reasoning { get; set; }

		[JsonProperty("thinkingChars")]
		public long ThinkingChars { get; set; }

		[JsonIgnore]
		public long Context => Input + CacheWrite + CacheRead;
	}
}
