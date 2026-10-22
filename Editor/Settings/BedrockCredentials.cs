using UnityEngine;

namespace DTech.Parley.Editor
{
    internal enum BedrockCredentials : byte
    {
        [InspectorName("Default AWS credential chain")]
        DefaultChain = 0,
        [InspectorName("AWS profile")]
        Profile = 1,
        [InspectorName("Access keys")]
        AccessKeys = 2,
        [InspectorName("Bedrock API key")]
        BearerToken = 3,
    }
}