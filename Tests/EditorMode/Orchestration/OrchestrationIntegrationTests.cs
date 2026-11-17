using System;
using System.Diagnostics;
using System.Reflection;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Tools;
using DTech.Parley.Editor.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class OrchestrationIntegrationTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void PermissionRestrictionDetachesAndKillsTheOldCliProcess(bool claude)
        {
            if (CommandLine.IsWindows) Assert.Ignore("Uses /bin/cat as a CLI process.");
            ParleyProfile profile = claude
                ? ParleyProfile.CreateClaude("Test", ClaudeAuthMethod.CliDefault)
                : ParleyProfile.CreateCodex("Test", CodexAuthMethod.CliDefault);
            ToolCatalog tools = new(Array.Empty<IParleyTool>());
            using IAgentBackend backend = claude
                ? new ClaudeCodeBackend(profile, new RecordingSink(), tools, null, PermissionMode.BypassPermissions)
                : new CodexBackend(profile, new RecordingSink(), tools, null, PermissionMode.BypassPermissions);
            ChildProcess process = new(new ProcessStartInfo("/bin/cat"));
            Assert.IsTrue(process.Start(out string error), error);
            backend.GetType().GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(backend, process);
            ((IAgentPermissionBoundary)backend).RestrictPermissions(PermissionMode.Plan);
            Assert.IsFalse(backend.IsRunning);
            Assert.IsTrue(process.WaitForExit(1000));
            Assert.AreEqual(PermissionMode.Plan, backend.Mode);
        }

        [Test]
        public void WorkflowKeepsStopAvailableAndPreservesDraftOnKeyboardSubmit()
        {
            Composer composer = new();
            TextField input = composer.Q<TextField>();
            input.value = "draft to preserve";
            int submissions = 0;
            composer.OnSubmit += (_, _) => submissions++;
            composer.SetBusy(false, true);
            Button stop = composer.Query<Button>().ToList().Find(button => button.text == "■ Stop");
            Button send = composer.Query<Button>().ToList().Find(button => button.text == "Working…");
            Assert.AreEqual(DisplayStyle.Flex, stop.style.display.value);
            Assert.IsFalse(send.enabledSelf);
            typeof(Composer).GetMethod("Submit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(composer, null);
            Assert.AreEqual("draft to preserve", input.value);
            Assert.AreEqual(0, submissions);
            composer.SetBusy(false, false);
            Assert.IsTrue(send.enabledSelf);
        }
    }
}
