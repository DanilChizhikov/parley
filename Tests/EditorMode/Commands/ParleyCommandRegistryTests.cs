using System.Collections.Generic;
using System.Text.RegularExpressions;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Commands;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ParleyCommandRegistryTests
    {
        [Test]
        public void BuildKeepsBuiltinWhenNameIsFree()
        {
            FakeParleyCommand builtin = new FakeParleyCommand { Name = "create-parley-panel" };
            FakeParleyCommand external = new FakeParleyCommand { Name = "hello" };
            List<IParleyCommand> commands = ParleyCommandRegistry.Build(new IParleyCommand[] { builtin }, new IParleyCommand[] { external });
            Assert.AreEqual(2, commands.Count);
            Assert.AreSame(external, commands[0]);
            Assert.AreSame(builtin, commands[1]);
        }

        [Test]
        public void ExternalCommandOverridesBuiltinWithTheSameName()
        {
            FakeParleyCommand builtin = new FakeParleyCommand { Name = "create-parley-panel", Prompt = "builtin" };
            FakeParleyCommand external = new FakeParleyCommand { Name = "create-parley-panel", Prompt = "external" };
            LogAssert.Expect(LogType.Warning, new Regex("overridden"));
            List<IParleyCommand> commands = ParleyCommandRegistry.Build(new IParleyCommand[] { builtin }, new IParleyCommand[] { external });
            Assert.AreEqual(1, commands.Count);
            Assert.AreSame(external, commands[0]);
        }

        [Test]
        public void BuildSkipsInvalidNames()
        {
            LogAssert.Expect(LogType.Warning, new Regex("invalid name"));
            List<IParleyCommand> commands = ParleyCommandRegistry.Build(null, new IParleyCommand[] { new FakeParleyCommand { Name = "Bad Name" } });
            Assert.AreEqual(0, commands.Count);
        }

        [Test]
        public void BuildKeepsOnlyTheFirstOfTwoExternalsWithTheSameName()
        {
            FakeParleyCommand first = new FakeParleyCommand { Name = "hello", Prompt = "first" };
            FakeParleyCommand second = new FakeParleyCommand { Name = "hello", Prompt = "second" };
            LogAssert.Expect(LogType.Warning, new Regex("already comes from"));
            List<IParleyCommand> commands = ParleyCommandRegistry.Build(null, new IParleyCommand[] { first, second });
            Assert.AreEqual(1, commands.Count);
            Assert.AreSame(first, commands[0]);
        }

        [Test]
        public void MergePutsCommandsFirstAndDropsBackendDuplicates()
        {
            List<SlashCommandInfo> target = new ();
            List<SlashCommandInfo> backend = new ()
            {
                new SlashCommandInfo { Name = CreatePanelCommand.CommandName, Description = "from the cli" },
                new SlashCommandInfo { Name = "clear", Description = "clear" },
            };

            ParleyCommandRegistry.Merge(target, backend);
            Assert.AreEqual(CreatePanelCommand.CommandName, target[0].Name);
            Assert.IsTrue(target[0].IsBuiltin);
            Assert.AreNotEqual("from the cli", target[0].Description);
            Assert.AreEqual(1, target.FindAll(command => command.Name == CreatePanelCommand.CommandName).Count);
            Assert.IsNotNull(target.Find(command => command.Name == "clear"));
        }

        [Test]
        public void MergeIsIdempotent()
        {
            List<SlashCommandInfo> target = new ();
            List<SlashCommandInfo> backend = new () { new SlashCommandInfo { Name = "clear" } };
            ParleyCommandRegistry.Merge(target, backend);
            int count = target.Count;
            ParleyCommandRegistry.Merge(target, backend);
            Assert.AreEqual(count, target.Count);
        }

        [Test]
        public void ExpandRendersArgumentsAndProjectRoot()
        {
            FakeParleyCommand command = new FakeParleyCommand { Name = "hello", Prompt = "do the thing" };
            List<IParleyCommand> commands = new () { command };
            Assert.AreEqual("do the thing", ParleyCommandRegistry.Expand("/hello tune goblin config", commands));
            Assert.AreEqual("tune goblin config", command.Request.Arguments);
            Assert.IsFalse(string.IsNullOrEmpty(command.Request.ProjectRoot));
        }

        [Test]
        public void ExpandReturnsNullWhenCommandThrows()
        {
            LogAssert.Expect(LogType.Warning, new Regex("failed"));
            Assert.IsNull(ParleyCommandRegistry.Expand("/throwing-command x", new List<IParleyCommand> { new ThrowingParleyCommand() }));
        }

        [Test]
        public void ExpandReturnsNullWhenPromptIsEmpty()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no prompt"));
            FakeParleyCommand command = new FakeParleyCommand { Name = "hello", Prompt = "  " };
            Assert.IsNull(ParleyCommandRegistry.Expand("/hello x", new List<IParleyCommand> { command }));
        }

        [Test]
        public void ExpandReturnsNullWhenNotACommand()
        {
            Assert.IsNull(ParleyCommandRegistry.Expand("/clear"));
            Assert.IsNull(ParleyCommandRegistry.Expand("make me a window"));
            Assert.IsNull(ParleyCommandRegistry.Expand(string.Empty));
        }
    }
}
