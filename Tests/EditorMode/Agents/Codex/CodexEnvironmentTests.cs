using System.Diagnostics;
using System.IO;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Codex;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexEnvironmentTests
    {
        [Test]
        public void ConfigDirWins()
        {
            ParleyProfile profile = ParleyProfile.CreateCodex("c", CodexAuthMethod.ApiKey);
            profile.ConfigDir = "~/codex-home";
            Assert.AreEqual(ProjectPaths.Resolve("~/codex-home"), CodexEnvironment.Home(profile));
        }

        [Test]
        public void CliDefaultKeepsCodexDefaultHome()
        {
            ParleyProfile profile = ParleyProfile.CreateCodex("c", CodexAuthMethod.CliDefault);
            Assert.IsNull(CodexEnvironment.Home(profile));
        }

        [Test]
        public void ApiKeyProfileGetsIsolatedHome()
        {
            ParleyProfile profile = ParleyProfile.CreateCodex("c", CodexAuthMethod.ApiKey);
            string home = CodexEnvironment.Home(profile);
            try
            {
                Assert.AreEqual(Path.Combine(ProjectPaths.LibraryFolder, "Codex", profile.Id), home);
                Assert.IsTrue(Directory.Exists(home));
            }
            finally
            {
                Directory.Delete(home, true);
            }
        }

        [Test]
        public void ApplySetsPathHomeAndDropsSandboxVariables()
        {
            ParleyProfile profile = ParleyProfile.CreateCodex("c", CodexAuthMethod.CliDefault);
            profile.ConfigDir = "~/codex-home";
            ProcessStartInfo startInfo = new ProcessStartInfo("codex");
            startInfo.Environment["CODEX_SANDBOX"] = "seatbelt";
            CodexEnvironment.Apply(startInfo, profile, "/custom/bin");
            Assert.AreEqual("/custom/bin", startInfo.Environment["PATH"]);
            Assert.IsFalse(startInfo.Environment.ContainsKey("CODEX_SANDBOX"));
            Assert.AreEqual(ProjectPaths.Resolve("~/codex-home"), startInfo.Environment["CODEX_HOME"]);
        }
    }
}