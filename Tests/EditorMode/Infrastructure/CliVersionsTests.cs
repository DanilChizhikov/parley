using System;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CliVersionsTests
    {
        [Test]
        public void ParsesCliVersion()
        {
            Assert.AreEqual(new Version(0, 162, 0), CliVersions.ParseVersion("codex-cli 0.162.0"));
            Assert.AreEqual(new Version(2, 1, 84), CliVersions.ParseVersion("2.1.84 (Claude Code)"));
            Assert.IsNull(CliVersions.ParseVersion("garbage"));
            Assert.IsNull(CliVersions.ParseVersion(null));
        }

        [Test]
        public void PicksNewestCandidate()
        {
            CliCandidate[] candidates =
            {
                new ("/home/user/.local/bin/claude", new Version(2, 1, 84)),
                new ("/opt/homebrew/bin/claude", new Version(2, 1, 308)),
                new ("/usr/local/bin/claude", null),
            };

            Assert.AreEqual("/opt/homebrew/bin/claude", CliVersions.PickNewest(candidates));
        }

        [Test]
        public void KeepsCandidateOrderWhenVersionsDoNotDecide()
        {
            CliCandidate[] unknown = { new ("/a/claude", null), new ("/b/claude", null) };
            Assert.AreEqual("/a/claude", CliVersions.PickNewest(unknown));

            CliCandidate[] equal = { new ("/a/claude", new Version(2, 1, 308)), new ("/b/claude", new Version(2, 1, 308)) };
            Assert.AreEqual("/a/claude", CliVersions.PickNewest(equal));

            Assert.IsNull(CliVersions.PickNewest(Array.Empty<CliCandidate>()));
        }

        [Test]
        public void FlagsOutdatedClaudeCli()
        {
            Assert.IsTrue(ClaudeCliLocator.IsOutdated(new Version(2, 1, 84)));
            Assert.IsFalse(ClaudeCliLocator.IsOutdated(new Version(2, 1, 308)));
            Assert.IsFalse(ClaudeCliLocator.IsOutdated(null));
        }
    }
}
