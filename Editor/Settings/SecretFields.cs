namespace DTech.Parley.Editor
{
    internal static class SecretFields
    {
        public const string ApiKey = "apiKey";
        public const string AuthToken = "authToken";
        public const string OAuthToken = "oauthToken";
        public const string AwsAccessKeyId = "awsAccessKeyId";
        public const string AwsSecretAccessKey = "awsSecretAccessKey";
        public const string AwsSessionToken = "awsSessionToken";
        public const string BedrockBearerToken = "bedrockBearerToken";
        public const string FoundryApiKey = "foundryApiKey";
        public const string LocalApiKey = "localApiKey";
        public const string OpenAiApiKey = "openAiApiKey";

        public static readonly string[] All =
        {
            ApiKey, AuthToken, OAuthToken, AwsAccessKeyId, AwsSecretAccessKey, AwsSessionToken, BedrockBearerToken, FoundryApiKey, LocalApiKey, OpenAiApiKey,
        };
    }
}