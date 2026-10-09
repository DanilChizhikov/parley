using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DTech.Parley.Editor
{
	internal sealed class TranscriptEntry
	{
		[JsonProperty("id")] public string Id = Guid.NewGuid().ToString("N");
		[JsonProperty("role")] public EntryRole Role;
		[JsonProperty("time")] public DateTime TimeUtc = DateTime.UtcNow;
		[JsonProperty("blocks")] public List<TranscriptBlock> Blocks = new ();
	}
}