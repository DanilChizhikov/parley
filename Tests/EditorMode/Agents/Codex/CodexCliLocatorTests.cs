using System.Collections.Generic;
using System.IO;
using DTech.Parley.Editor.Agents.Codex;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexCliLocatorTests
    {
        [Test]
        public void CandidatesCoverCommonInstallLocations()
        {
            List<string> unix = new (CodexCliLocator.Candidates("/home/user", false));
            CollectionAssert.Contains(unix, Path.Combine("/home/user", ".local", "bin", "codex"));
            CollectionAssert.Contains(unix, "/opt/homebrew/bin/codex");

            List<string> windows = new (CodexCliLocator.Candidates("C:/Users/user", true));
            StringAssert.EndsWith("codex.cmd", windows[0]);
        }
    }
}