using DTech.Parley.Editor.UI;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class InlineMarkdownTests
    {
        [Test]
        public void InlineFormattingEscapesTags()
        {
            string rich = InlineMarkdown.ToRichText("a <b>tag</b> and **bold** and `x<y`");
            StringAssert.Contains("<noparse><</noparse>b>", rich);
            StringAssert.Contains("<b>bold</b>", rich);
            StringAssert.Contains("x<noparse><</noparse>y", rich);
        }

        [Test]
        public void InlineLinksBecomeLinkTags()
        {
            string rich = InlineMarkdown.ToRichText("see [docs](https://example.com) or https://unity.com.");
            StringAssert.Contains("<link=\"https://example.com\">", rich);
            StringAssert.Contains("<link=\"https://unity.com\">", rich);
        }

        [Test]
        public void SnakeCaseIsNotItalic()
        {
            string rich = InlineMarkdown.ToRichText("call my_function_name now");
            StringAssert.DoesNotContain("<i>", rich);
        }

        [Test]
        public void CodePathsBecomeFileLinks()
        {
            string rich = InlineMarkdown.ToRichText("open `Assets/Scripts/Player.cs:12`");
            StringAssert.Contains("<link=\"file:Assets/Scripts/Player.cs:12\">", rich);
        }
    }
}
