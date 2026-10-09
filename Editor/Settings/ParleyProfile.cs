using System;
using UnityEngine;

namespace DTech.Parley.Editor
{
	[Serializable]
	internal sealed class ParleyProfile
	{
		[field: SerializeField]
		public string Id { get; set; } = Guid.NewGuid().ToString("N");
		
		[field: SerializeField]
		public string Name { get; set; } = "Profile";
		
		[field: SerializeField]
		public ProfileKind Kind { get; set; }

		[field: SerializeField]
		public ClaudeAuthMethod AuthMethod { get; set; }
		
		[field: SerializeField]
		public string ConfigDir { get; set; }
		
		[field: SerializeField]
		public string LoginEmail { get; set; }
		
		[field: SerializeField]
		public string BaseUrl { get; set; }
		
		[field: SerializeField]
		public string CustomHeaders { get; set; }
		
		[field: SerializeField]
		public string ApiKeyHelper { get; set; }
		
		[field: SerializeField]
		public string AwsRegion { get; set; }
		
		[field: SerializeField]
		public string AwsProfile { get; set; }
		
		[field: SerializeField]
		public BedrockCredentials BedrockCredentials { get; set; }
		
		[field: SerializeField]
		public string BedrockBaseUrl { get; set; }
		
		[field: SerializeField]
		public string VertexRegion { get; set; }
		
		[field: SerializeField]
		public string VertexProjectId { get; set; }
		
		[field: SerializeField]
		public string GoogleCredentialsPath { get; set; }
		
		[field: SerializeField]
		public string FoundryResource { get; set; }
		
		[field: SerializeField]
		public string FoundryBaseUrl { get; set; }
		
		[field: SerializeField]
		public bool FoundryUseEntraId { get; set; }
		
		[field: SerializeField]
		public string AnthropicProfile { get; set; }
		
		[field: SerializeField]
		public string Model { get; set; }
		
		[field: SerializeField]
		public string Effort { get; set; }
		
		[field: SerializeField]
		public string ExtraArguments { get; set; }
		
		[field: SerializeField]
		public LocalPreset Preset { get; set; }
		
		[field: SerializeField]
		public string LocalBaseUrl { get; set; }
		
		[field: SerializeField, Min(0f)]
		public float Temperature { get; set; } = 0.2f;
		
		[field: SerializeField, Min(1)]
		public int MaxTokens { get; set; } = 4096;
		
		[field: SerializeField, Min(1)]
		public int ContextWindow { get; set; } = 32768;
		
		[field: SerializeField, Min(1)]
		public int MaxTurns { get; set; } = 40;
		
		[field: SerializeField]
		public bool Vision { get; set; }
		
		[field: SerializeField]
		public bool TextToolCalls { get; set; }

		public static ParleyProfile CreateClaude(string name, ClaudeAuthMethod method) => new ParleyProfile
			{ Name = name, Kind = ProfileKind.ClaudeCode, AuthMethod = method };

		public static ParleyProfile CreateLocal(string name, LocalPreset preset)
		{
			return new ParleyProfile
			{
				Name = name,
				Kind = ProfileKind.Local,
				Preset = preset,
				LocalBaseUrl = LocalPresets.DefaultBaseUrl(preset),
			};
		}

		public ParleyProfile Clone()
		{
			ParleyProfile copy = (ParleyProfile)MemberwiseClone();
			copy.Id = Guid.NewGuid().ToString("N");
			copy.Name = Name + " copy";
			return copy;
		}
	}
}