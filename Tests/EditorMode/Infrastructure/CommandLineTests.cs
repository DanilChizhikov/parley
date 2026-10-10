using System.Collections.Generic;
using DTech.Parley.Editor;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CommandLineTests
    {
        [Test]
        public void Quoting()
        {
            Assert.AreEqual("plain", CommandLine.QuoteWindows("plain"));
            Assert.AreEqual("\"a b\"", CommandLine.QuoteWindows("a b"));
            Assert.AreEqual("\"say \\\"hi\\\"\"", CommandLine.QuoteWindows("say \"hi\""));
            Assert.AreEqual("\"C:\\dir name\\\\\"", CommandLine.QuoteWindows("C:\\dir name\\"));
            Assert.AreEqual("--resume=abc-123", CommandLine.QuoteUnix("--resume=abc-123"));
            Assert.AreEqual("\"a \\$HOME \\\"x\\\"\"", CommandLine.QuoteUnix("a $HOME \"x\""));
        }

        [Test]
        public void SplitHonorsQuotes()
        {
            List<string> arguments = new (CommandLine.Split("-c model=\"gpt 5\"  --verbose"));
            CollectionAssert.AreEqual(new[] { "-c", "model=gpt 5", "--verbose" }, arguments);
            CollectionAssert.IsEmpty(CommandLine.Split("   "));
        }
    }
}