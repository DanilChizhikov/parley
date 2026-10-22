using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageTool
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("agent")]
		public string Agent { get; set; }

		[JsonProperty("call")]
		public string CallId { get; set; }

		[JsonProperty("target")]
		public string Target { get; set; }

		[JsonProperty("inputChars")]
		public long InputChars { get; set; }

		[JsonProperty("resultChars")]
		public long ResultChars { get; set; }

		[JsonProperty("error")]
		public bool IsError { get; set; }
	}
}
