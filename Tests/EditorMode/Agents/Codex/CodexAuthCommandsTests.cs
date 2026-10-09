using DTech.Parley.Editor.Agents.Codex;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexAuthCommandsTests
    {
        [Test]
        public void LoginArgumentsSupportDeviceCode()
        {
            CollectionAssert.AreEqual(new[] { "login" }, CodexAuthCommands.LoginArguments(false));
            CollectionAssert.AreEqual(new[] { "login", "--device-auth" }, CodexAuthCommands.LoginArguments(true));
        }

        [Test]
        public void StatusSummaryPrefersErrorThenText()
        {
            Assert.AreEqual("boom", new CodexAuthStatus { Error = "boom", Text = "Logged in using ChatGPT" }.Summary);
            Assert.AreEqual("Logged in using ChatGPT", new CodexAuthStatus { LoggedIn = true, Text = "Logged in using ChatGPT" }.Summary);
            Assert.AreEqual("Not signed in", new CodexAuthStatus().Summary);
        }
    }
}