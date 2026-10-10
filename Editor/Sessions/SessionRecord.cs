using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class SessionRecord
	{
		public const string DefaultTitle = "New chat";

		[JsonProperty("id")]
		public string Id { get; set; } = Guid.NewGuid().ToString("N");

		[JsonProperty("title")]
		public string Title { get; set; } = DefaultTitle;

		[JsonProperty("profileId")]
		public string ProfileId { get; set; }

		[JsonProperty("kind")]
		public ProfileKind Kind { get; set; }

		[JsonProperty("backendSessionId")]
		public string BackendSessionId { get; set; }

		[JsonProperty("created")]
		public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

		[JsonProperty("updated")]
		public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

		[JsonProperty("costUsd")]
		public double CostUsd { get; set; }

		[JsonProperty("plan")]
		public string LatestPlan { get; set; }

		[JsonProperty("mode")]
		public PermissionMode Mode { get; set; }

		[JsonProperty("entries")]
		public List<TranscriptEntry> Entries { get; set; } = new();

		[JsonProperty("history")]
		public List<JObject> LocalHistory { get; set; }

		[JsonProperty("mcpEnabled")]
		public List<string> EnabledMcpServerIds { get; set; }

		[JsonProperty("mcpDisabledExternal")]
		public List<string> DisabledExternalMcpServers { get; set; }

		[JsonProperty("skillsEnabled")]
		public List<string> EnabledLibrarySkills { get; set; }

		[JsonProperty("skillsDisabledExternal")]
		public List<string> DisabledExternalSkills { get; set; }

		[JsonProperty("autoSkills")]
		public List<SkillInvocation> PendingAutoSkills { get; set; }
	}
}