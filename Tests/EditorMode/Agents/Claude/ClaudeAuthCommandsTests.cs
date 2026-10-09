using DTech.Parley.Editor.Agents.Claude;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ClaudeAuthCommandsTests
    {
        [Test]
        public void AuthStatusJsonIsParsed()
        {
            AuthStatus status = ClaudeAuthCommands.Parse("{\"loggedIn\":true,\"authMethod\":\"claude.ai\",\"apiProvider\":\"firstParty\",\"subscriptionType\":\"max\"}", string.Empty);
            Assert.IsTrue(status.LoggedIn);
            StringAssert.Contains("claude.ai", status.Summary);
        }

        [Test]
        public void TokensAndUrlsAreExtractedFromAnsiOutput()
        {
            Assert.AreEqual("sk-ant-oat01-abc_DEF-1", ClaudeAuthCommands.ExtractToken("\u001b[32mToken: sk-ant-oat01-abc_DEF-1\u001b[0m"));
            Assert.AreEqual("https://claude.ai/oauth?x=1", ClaudeAuthCommands.ExtractUrl("Open https://claude.ai/oauth?x=1 to continue"));
        }
    }
}