using System.Collections.Generic;
using DTech.Parley.Editor.UI;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class MarkdownParserTests
    {
        [Test]
        public void ParsesCommonBlocks()
        {
            string markdown = "# Title\n\nSome **bold** text\nnext line\n\n- one\n- [x] two\n  more\n\n```cs\nvar a = 1;\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n> quote\n\n---";
            List<MarkdownBlock> blocks = MarkdownParser.Parse(markdown);
            Assert.AreEqual(MarkdownBlockType.Heading, blocks[0].Type);
            Assert.AreEqual(MarkdownBlockType.Paragraph, blocks[1].Type);
            Assert.AreEqual("Some **bold** text\nnext line", blocks[1].Text);
            Assert.AreEqual(MarkdownBlockType.List, blocks[2].Type);
            Assert.AreEqual(true, blocks[2].Items[1].Checked);
            Assert.AreEqual("two\nmore", blocks[2].Items[1].Text);
            Assert.AreEqual(MarkdownBlockType.Code, blocks[3].Type);
            Assert.AreEqual("cs", blocks[3].Language);
            Assert.AreEqual("var a = 1;", blocks[3].Text);
            Assert.AreEqual(MarkdownBlockType.Table, blocks[4].Type);
            Assert.AreEqual(2, blocks[4].Rows.Count);
            Assert.AreEqual(MarkdownBlockType.Quote, blocks[5].Type);
            Assert.AreEqual(MarkdownBlockType.Rule, blocks[6].Type);
        }

        [Test]
        public void UnterminatedFenceIsStillCode()
        {
            List<MarkdownBlock> blocks = MarkdownParser.Parse("text\n```json\n{\"a\":");
            Assert.AreEqual(MarkdownBlockType.Code, blocks[1].Type);
            Assert.IsFalse(blocks[1].IsClosed);
        }

        [Test]
        public void HeadingKeepsTrailingHashInText()
        {
            Assert.AreEqual("Using C#", MarkdownParser.Parse("## Using C#")[0].Text);
            Assert.AreEqual("Title", MarkdownParser.Parse("## Title ##")[0].Text);
        }

        [Test]
        public void TableRowsKeepEscapedPipes()
        {
            CollectionAssert.AreEqual(new[] { "a|b", "c" }, MarkdownParser.SplitRow("| a\\|b | c |"));
        }
    }
}
