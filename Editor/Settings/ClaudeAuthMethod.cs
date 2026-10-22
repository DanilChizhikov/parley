using UnityEngine;

namespace DTech.Parley.Editor
{
    internal enum ClaudeAuthMethod : byte
    {
        [InspectorName("Claude Code default")]
        CliDefault = 0,
        [InspectorName("Claude.ai subscription")]
        ClaudeAiLogin = 1,
        [InspectorName("Claude Console account")]
        ConsoleLogin = 2,
        [InspectorName("Single sign-on (SSO)")]
        SsoLogin = 3,
        [InspectorName("OAuth token")]
        OAuthToken = 4,
        [InspectorName("API key")]
        ApiKey = 5,
        [InspectorName("LLM gateway / custom URL")]
        Gateway = 6,
        [InspectorName("API key helper script")]
        ApiKeyHelper = 7,
        [InspectorName("Amazon Bedrock")]
        Bedrock = 8,
        [InspectorName("Google Vertex AI")]
        Vertex = 9,
        [InspectorName("Microsoft Foundry")]
        Foundry = 10,
        [InspectorName("Anthropic profile")]
        AnthropicProfile = 11,
    }
}