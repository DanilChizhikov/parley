using System;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class SessionSummary
	{
		[JsonProperty("id")]
		public string Id { get; set; }
		
		[JsonProperty("title")]
		public string Title { get; set; }
		
		[JsonProperty("profileId")]
		public string ProfileId { get; set; }
		
		[JsonProperty("updated")]
		public DateTime UpdatedUtc { get; set; }
	}
}