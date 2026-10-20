using System.Collections.Generic;
using DTech.Parley.Editor.Commands;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ParleyCommandParserTests
    {
        private static IReadOnlyList<IParleyCommand> Commands()
        {
            return new List<IParleyCommand> { new FakeParleyCommand { Name = "create-parley-panel" } };
        }

        [Test]
        public void SplitsNameAndArguments()
        {
            IParleyCommand command = ParleyCommandParser.Match("/create-parley-panel tune goblin config", Commands(), out string arguments);
            Assert.IsNotNull(command);
            Assert.AreEqual("tune goblin config", arguments);
        }

        [Test]
        public void AcceptsEmptyArguments()
        {
            Assert.IsNotNull(ParleyCommandParser.Match("/create-parley-panel", Commands(), out string arguments));
            Assert.AreEqual(string.Empty, arguments);
            Assert.IsNotNull(ParleyCommandParser.Match("  /create-parley-panel   ", Commands(), out string padded));
            Assert.AreEqual(string.Empty, padded);
        }

        [Test]
        public void KeepsMultilineArguments()
        {
            IParleyCommand command = ParleyCommandParser.Match("/create-parley-panel first\nsecond", Commands(), out string arguments);
            Assert.IsNotNull(command);
            StringAssert.Contains("\nsecond", arguments);
        }

        [Test]
        public void IsCaseInsensitive()
        {
            Assert.IsNotNull(ParleyCommandParser.Match("/Create-Parley-Panel x", Commands(), out _));
        }

        [Test]
        public void IgnoresUnregisteredNames()
        {
            Assert.IsNull(ParleyCommandParser.Match("/clear", Commands(), out _));
            Assert.IsNull(ParleyCommandParser.Match("/compact", Commands(), out _));
            Assert.IsNull(ParleyCommandParser.Match("/create-parley-panelish x", Commands(), out _));
        }

        [Test]
        public void IgnoresCommandsMidSentence()
        {
            Assert.IsNull(ParleyCommandParser.Match("do this /create-parley-panel x", Commands(), out _));
        }

        [Test]
        public void IgnoresPlainAndEmptyText()
        {
            Assert.IsNull(ParleyCommandParser.Match(null, Commands(), out _));
            Assert.IsNull(ParleyCommandParser.Match(string.Empty, Commands(), out _));
            Assert.IsNull(ParleyCommandParser.Match("/", Commands(), out _));
            Assert.IsNull(ParleyCommandParser.Match("hello", Commands(), out _));
        }

        [Test]
        public void IsValidNameRejectsUppercaseAndLeadingDash()
        {
            Assert.IsTrue(ParleyCommandParser.IsValidName("create-parley-panel"));
            Assert.IsTrue(ParleyCommandParser.IsValidName("panel2"));
            Assert.IsFalse(ParleyCommandParser.IsValidName("MyCommand"));
            Assert.IsFalse(ParleyCommandParser.IsValidName("-panel"));
            Assert.IsFalse(ParleyCommandParser.IsValidName("my panel"));
            Assert.IsFalse(ParleyCommandParser.IsValidName("my_panel"));
            Assert.IsFalse(ParleyCommandParser.IsValidName(string.Empty));
        }
    }
}
