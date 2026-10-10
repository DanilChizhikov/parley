using DTech.Parley.Editor.Agents.Claude;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ClaudeCodeBackendTests
    {
        [Test]
        public void DetectsRestrictedMode()
        {
            Assert.IsTrue(ClaudeCodeBackend.IsRestrictedMode("--fallback-model sonnet --restricted", null));
            Assert.IsTrue(ClaudeCodeBackend.IsRestrictedMode(null, "1"));
            Assert.IsTrue(ClaudeCodeBackend.IsRestrictedMode(string.Empty, " TRUE "));
            Assert.IsFalse(ClaudeCodeBackend.IsRestrictedMode("--fallback-model sonnet", "0"));
            Assert.IsFalse(ClaudeCodeBackend.IsRestrictedMode(null, null));
        }
    }
}
