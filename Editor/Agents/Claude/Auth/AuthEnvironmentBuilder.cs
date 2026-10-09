using System;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal static class AuthEnvironmentBuilder
	{
		public static readonly string[] CredentialVariables =
		{
			"ANTHROPIC_API_KEY",
			"ANTHROPIC_AUTH_TOKEN",
			"ANTHROPIC_BASE_URL",
			"ANTHROPIC_CUSTOM_HEADERS",
			"ANTHROPIC_PROFILE",
			"CLAUDE_CODE_OAUTH_TOKEN",
			"CLAUDE_CODE_USE_BEDROCK",
			"CLAUDE_CODE_USE_VERTEX",
			"CLAUDE_CODE_USE_FOUNDRY",
			"CLAUDE_CODE_USE_MANTLE",
			"AWS_BEARER_TOKEN_BEDROCK",
			"ANTHROPIC_FOUNDRY_API_KEY",
			"ANTHROPIC_FOUNDRY_AUTH_TOKEN",
		};

		public static AuthEnvironment Build(ParleyProfile profile, Func<string, string> readSecret)
		{
			AuthEnvironment environment = new AuthEnvironment();
			if (!string.IsNullOrEmpty(profile.ConfigDir))
			{
				environment.Variables["CLAUDE_CONFIG_DIR"] = ProjectPaths.Resolve(profile.ConfigDir);
			}

			switch (profile.AuthMethod)
			{
				case ClaudeAuthMethod.CliDefault:
					return environment;
				case ClaudeAuthMethod.ClaudeAiLogin:
				case ClaudeAuthMethod.ConsoleLogin:
				case ClaudeAuthMethod.SsoLogin:
					break;
				case ClaudeAuthMethod.OAuthToken:
					environment.Require("CLAUDE_CODE_OAUTH_TOKEN", readSecret(SecretFields.OAuthToken), "OAuth token");
					break;
				case ClaudeAuthMethod.ApiKey:
					environment.Require("ANTHROPIC_API_KEY", readSecret(SecretFields.ApiKey), "API key");
					break;
				case ClaudeAuthMethod.Gateway:
					environment.Require("ANTHROPIC_BASE_URL", profile.BaseUrl, "Base URL");
					environment.Optional("ANTHROPIC_AUTH_TOKEN", readSecret(SecretFields.AuthToken));
					environment.Optional("ANTHROPIC_CUSTOM_HEADERS", profile.CustomHeaders);
					break;
				case ClaudeAuthMethod.ApiKeyHelper:
					if (string.IsNullOrWhiteSpace(profile.ApiKeyHelper))
					{
						environment.Problems.Add("apiKeyHelper command is empty.");
					}
					else
					{
						environment.Settings["apiKeyHelper"] = profile.ApiKeyHelper;
					}

					break;
				case ClaudeAuthMethod.Bedrock:
					BuildBedrock(profile, readSecret, environment);
					break;
				case ClaudeAuthMethod.Vertex:
					environment.Variables["CLAUDE_CODE_USE_VERTEX"] = "1";
					environment.Require("CLOUD_ML_REGION", profile.VertexRegion, "Vertex region");
					environment.Require("ANTHROPIC_VERTEX_PROJECT_ID", profile.VertexProjectId, "Vertex project id");
					string credentialsPath = string.IsNullOrEmpty(profile.GoogleCredentialsPath) ? null : ProjectPaths.Resolve(profile.GoogleCredentialsPath);
					environment.Optional("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
					break;
				case ClaudeAuthMethod.Foundry:
					environment.Variables["CLAUDE_CODE_USE_FOUNDRY"] = "1";
					if (string.IsNullOrEmpty(profile.FoundryResource) && string.IsNullOrEmpty(profile.FoundryBaseUrl))
					{
						environment.Problems.Add("Foundry resource name or base URL is required.");
					}

					environment.Optional("ANTHROPIC_FOUNDRY_RESOURCE", profile.FoundryResource);
					environment.Optional("ANTHROPIC_FOUNDRY_BASE_URL", profile.FoundryBaseUrl);
					if (!profile.FoundryUseEntraId)
					{
						environment.Require("ANTHROPIC_FOUNDRY_API_KEY", readSecret(SecretFields.FoundryApiKey), "Foundry API key");
					}

					break;
				case ClaudeAuthMethod.AnthropicProfile:
					environment.Require("ANTHROPIC_PROFILE", profile.AnthropicProfile, "Anthropic profile name");
					break;
			}

			foreach (string variable in CredentialVariables)
			{
				if (!environment.Variables.ContainsKey(variable))
				{
					environment.Removed.Add(variable);
				}
			}

			return environment;
		}

		public static string Describe(ClaudeAuthMethod method)
		{
			return method switch
			{
				ClaudeAuthMethod.CliDefault => "Claude Code default (whatever the CLI is signed in with)",
				ClaudeAuthMethod.ClaudeAiLogin => "Claude subscription (Pro / Max / Team / Enterprise)",
				ClaudeAuthMethod.ConsoleLogin => "Claude Console account (API billing)",
				ClaudeAuthMethod.SsoLogin => "Single sign-on",
				ClaudeAuthMethod.OAuthToken => "Long-lived OAuth token (claude setup-token)",
				ClaudeAuthMethod.ApiKey => "Anthropic API key",
				ClaudeAuthMethod.Gateway => "LLM gateway / custom endpoint (ANTHROPIC_BASE_URL)",
				ClaudeAuthMethod.ApiKeyHelper => "apiKeyHelper script",
				ClaudeAuthMethod.Bedrock => "Amazon Bedrock",
				ClaudeAuthMethod.Vertex => "Google Cloud Vertex AI",
				ClaudeAuthMethod.Foundry => "Microsoft Foundry",
				ClaudeAuthMethod.AnthropicProfile => "Anthropic profile / Workload Identity Federation",
				_ => method.ToString(),
			};
		}

		public static bool UsesInteractiveLogin(ClaudeAuthMethod method)
		{
			return method == ClaudeAuthMethod.ClaudeAiLogin
				|| method == ClaudeAuthMethod.ConsoleLogin
				|| method == ClaudeAuthMethod.SsoLogin
				|| method == ClaudeAuthMethod.CliDefault;
		}

		private static void BuildBedrock(ParleyProfile profile, Func<string, string> readSecret, AuthEnvironment environment)
		{
			environment.Variables["CLAUDE_CODE_USE_BEDROCK"] = "1";
			environment.Require("AWS_REGION", profile.AwsRegion, "AWS region");
			environment.Optional("ANTHROPIC_BEDROCK_BASE_URL", profile.BedrockBaseUrl);
			switch (profile.BedrockCredentials)
			{
				case BedrockCredentials.Profile:
					environment.Require("AWS_PROFILE", profile.AwsProfile, "AWS profile");
					break;
				case BedrockCredentials.AccessKeys:
					environment.Require("AWS_ACCESS_KEY_ID", readSecret(SecretFields.AwsAccessKeyId), "AWS access key id");
					environment.Require("AWS_SECRET_ACCESS_KEY", readSecret(SecretFields.AwsSecretAccessKey), "AWS secret access key");
					environment.Optional("AWS_SESSION_TOKEN", readSecret(SecretFields.AwsSessionToken));
					break;
				case BedrockCredentials.BearerToken:
					environment.Require("AWS_BEARER_TOKEN_BEDROCK", readSecret(SecretFields.BedrockBearerToken), "Bedrock API key");
					break;
			}
		}
	}
}