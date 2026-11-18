using System;
using System.Threading;
using DTech.Parley.Editor.Orchestration;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class DelegationParserTests
    {
        private const string Tag = "<parley_delegate target=\"reviewer\" mode=\"summary\">Review &amp; test</parley_delegate>";

        [Test]
        public void HandlesEveryChunkBoundaryWithoutDuplicateRequests()
        {
            for (int split = 0; split <= Tag.Length; split++)
            {
                DelegationStreamParser parser = new();
                parser.Feed(Tag.Substring(0, split));
                parser.Feed(Tag.Substring(split));
                var requests = parser.Complete();
                Assert.AreEqual(1, requests.Count, "split " + split);
                Assert.AreEqual("Review & test", requests[0].TaskSummary);
                Assert.AreEqual(1, parser.Complete().Count);
            }
        }

        [TestCase("```xml\n" + Tag + "\n```", 0)]
        [TestCase("~~~xml\n" + Tag + "\n~~~", 0)]
        [TestCase("````xml\n```\n" + Tag + "\n````", 0)]
        [TestCase("Example: " + Tag, 0)]
        [TestCase("> " + Tag, 0)]
        [TestCase("    " + Tag, 0)]
        [TestCase("\t" + Tag, 0)]
        [TestCase("`" + Tag + "`", 0)]
        [TestCase("```xml\n" + Tag + "\n```\n" + Tag, 1)]
        public void IgnoresQuotedAndFencedExamples(string text, int expected)
        {
            DelegationStreamParser parser = new();
            foreach (char value in text) parser.Feed(value.ToString());
            Assert.AreEqual(expected, parser.Complete().Count);
        }

        [TestCase("<parley_delegate target=\"reviewer\" mode=\"summary\">unfinished")]
        [TestCase("<parley_delegate target=\"reviewer\" mode=\"full\">task</parley_delegate>")]
        [TestCase("<parley_delegate target=\"reviewer\" mode=\"summary\"><task>nested</task></parley_delegate>")]
        [TestCase("<parley_delegate target=\"reviewer\" mode=\"summary\" extra=\"x\">task</parley_delegate>")]
        [TestCase("<parley_delegate target=\"reviewer\" mode=\"summary\">&broken;</parley_delegate>")]
        [TestCase("<parley_delegate target=\"reviewer\" mode=\"summary\"></parley_delegate>")]
        [TestCase("<parley_delegate target=\"reviewer\" mode=\"summary\">task</parley_delegate> trailing")]
        public void MalformedTagsNeverDispatch(string text)
        {
            DelegationStreamParser parser = new();
            parser.Feed(text);
            Assert.IsEmpty(parser.Complete());
        }

        [Test]
        public void OversizedPayloadAndCancellationFailClosed()
        {
            DelegationStreamParser oversized = new();
            oversized.Feed("<parley_delegate target=\"reviewer\" mode=\"summary\">" + new string('x', 70000));
            oversized.Feed("</parley_delegate>\n");
            Assert.IsEmpty(oversized.Complete());
            DelegationStreamParser cancelled = new();
            cancelled.Feed(Tag + "\n");
            cancelled.Cancel();
            cancelled.Feed(Tag);
            Assert.IsEmpty(cancelled.Complete());
        }

        [Test]
        public void NativeToolValidatesSchemaAndCancellationBeforeDispatch()
        {
            int calls = 0;
            DelegateToAgentTool tool = new(request => { calls++; return request.RequestId; });
            ToolContext context = new() { ToolUseId = "native-7" };
            JObject valid = new()
            {
                ["target_skill"] = "reviewer", ["task_summary"] = "review", ["context_mode"] = "summary",
            };
            Assert.AreEqual("native-7", tool.ExecuteAsync(valid, context, CancellationToken.None).Result.Text);
            valid["task_summary"] = new JObject();
            Assert.IsTrue(tool.ExecuteAsync(valid, context, CancellationToken.None).Result.IsError);
            Assert.AreEqual(1, calls);
            Assert.Throws<OperationCanceledException>(() =>
                tool.ExecuteAsync(valid, context, new CancellationToken(true)));
            Assert.AreEqual(1, calls);
        }
    }
}
