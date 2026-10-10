using System.Collections.Generic;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class AuthEnvironmentBuilderTests
    {
        [Test]
        public void ApiKeyProfileSetsKeyAndStripsOtherCredentials()
        {
            ParleyProfile profile = ParleyProfile.CreateClaude("k", ClaudeAuthMethod.ApiKey);
            AuthEnvironment environment = AuthEnvironmentBuilder.Build(profile, field => field == SecretFields.ApiKey ? "sk-test" : null);
            Assert.IsTrue(environment.IsValid);
            Assert.AreEqual("sk-test", environment.Variables["ANTHROPIC_API_KEY"]);
            CollectionAssert.Contains(environment.Removed, "ANTHROPIC_AUTH_TOKEN");
            CollectionAssert.Contains(environment.Removed, "CLAUDE_CODE_USE_BEDROCK");
            CollectionAssert.DoesNotContain(environment.Removed, "ANTHROPIC_API_KEY");
        }

        [Test]
        public void MissingSecretIsReported()
        {
            ParleyProfile profile = ParleyProfile.CreateClaude("t", ClaudeAuthMethod.OAuthToken);
            AuthEnvironment environment = AuthEnvironmentBuilder.Build(profile, _ => null);
            Assert.IsFalse(environment.IsValid);
        }

        [Test]
        public void CliDefaultLeavesEnvironmentAlone()
        {
            ParleyProfile profile = ParleyProfile.CreateClaude("d", ClaudeAuthMethod.CliDefault);
            AuthEnvironment environment = AuthEnvironmentBuilder.Build(profile, _ => null);
            Assert.AreEqual(0, environment.Removed.Count);
            Assert.AreEqual(0, environment.Variables.Count);
        }

        [Test]
        public void BedrockAccessKeysAndHelperSettings()
        {
            ParleyProfile bedrock = ParleyProfile.CreateClaude("b", ClaudeAuthMethod.Bedrock);
            bedrock.AwsRegion = "us-east-1";
            bedrock.BedrockCredentials = BedrockCredentials.AccessKeys;
            Dictionary<string, string> secrets = new () { [SecretFields.AwsAccessKeyId] = "AKIA", [SecretFields.AwsSecretAccessKey] = "secret" };
            AuthEnvironment environment = AuthEnvironmentBuilder.Build(bedrock, field => secrets.TryGetValue(field, out string value) ? value : null);
            Assert.IsTrue(environment.IsValid);
            Assert.AreEqual("1", environment.Variables["CLAUDE_CODE_USE_BEDROCK"]);
            Assert.AreEqual("AKIA", environment.Variables["AWS_ACCESS_KEY_ID"]);

            ParleyProfile helper = ParleyProfile.CreateClaude("h", ClaudeAuthMethod.ApiKeyHelper);
            helper.ApiKeyHelper = "~/key.sh";
            Assert.AreEqual("~/key.sh", (string)AuthEnvironmentBuilder.Build(helper, _ => null).Settings["apiKeyHelper"]);
        }
    }
}