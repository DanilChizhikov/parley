namespace DTech.Parley.Editor
{
    internal enum ClaudeAuthMethod : byte
    {
        CliDefault = 0,
        ClaudeAiLogin = 1,
        ConsoleLogin = 2,
        SsoLogin = 3,
        OAuthToken = 4,
        ApiKey = 5,
        Gateway = 6,
        ApiKeyHelper = 7,
        Bedrock = 8,
        Vertex = 9,
        Foundry = 10,
        AnthropicProfile = 11,
    }
}