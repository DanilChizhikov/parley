using DTech.Parley.Editor.Commands;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CreatePanelCommandTests
    {
        private static string Prompt(string arguments)
        {
            return new CreatePanelCommand().BuildPrompt(new ParleyCommandRequest(arguments, "/project"));
        }

        [Test]
        public void PromptLoads()
        {
            Assert.IsFalse(string.IsNullOrEmpty(Prompt("tune goblin config")));
        }

        [Test]
        public void PromptCarriesTheDescription()
        {
            string prompt = Prompt("tune goblin config");
            StringAssert.Contains("tune goblin config", prompt);
            StringAssert.DoesNotContain("$ARGUMENTS", prompt);
        }

        [Test]
        public void PromptEmbedsTheSpecReference()
        {
            string prompt = Prompt("tune goblin config");
            StringAssert.Contains("Panel spec (JSON object)", prompt);
            StringAssert.DoesNotContain("${PANEL_SPEC}", prompt);
        }

        [Test]
        public void PromptDrivesTheToolWorkflow()
        {
            string prompt = Prompt("tune goblin config");
            StringAssert.Contains("panel_open", prompt);
            StringAssert.Contains("inspect", prompt);
        }

        [Test]
        public void PromptForbidsSaving()
        {
            string prompt = Prompt("tune goblin config");
            StringAssert.Contains("Do not call `panel_save`", prompt);
            StringAssert.Contains("Save button", prompt);
        }

        [Test]
        public void EmptyArgumentsStillRender()
        {
            string prompt = Prompt(string.Empty);
            Assert.IsFalse(string.IsNullOrEmpty(prompt));
            StringAssert.DoesNotContain("$ARGUMENTS", prompt);
        }

        [Test]
        public void PromptPrefersQueryForAssetFamilies()
        {
            string prompt = Prompt("all the brains");
            StringAssert.Contains("repeat", prompt);
            StringAssert.Contains("query", prompt);
            StringAssert.Contains("heal", prompt);
        }

        [Test]
        public void IsDiscoveredByRegistry()
        {
            ParleyCommandRegistry.Refresh();
            Assert.IsNotNull(ParleyCommandRegistry.Find(CreatePanelCommand.CommandName));
        }
    }
}
