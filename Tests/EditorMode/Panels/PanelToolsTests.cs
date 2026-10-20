using System.Threading;
using DTech.Parley.Editor.Tools;
using DTech.Parley.Editor.Tools.UnityTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelToolsTests
    {
        [Test]
        public void OpenReturnsValidationErrorsWithoutOpening()
        {
            ToolResult result = Run(new PanelOpenTool(), @"{ ""spec"": { ""title"": ""T"", ""items"": [ { ""type"": ""slider"" } ] } }");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("nothing opened", result.Text);
            StringAssert.Contains("Unknown type 'slider'", result.Text);
        }

        [Test]
        public void OpenNeedsExactlyOneOfSpecOrName()
        {
            Assert.IsTrue(Run(new PanelOpenTool(), "{}").IsError);
            ToolResult both = Run(new PanelOpenTool(), @"{ ""name"": ""a"", ""spec"": { ""title"": ""T"", ""items"": [] } }");
            Assert.IsTrue(both.IsError);
            StringAssert.Contains("not both", both.Text);
        }

        [Test]
        public void OpenRejectsSpecThatIsNotAnObject()
        {
            ToolResult result = Run(new PanelOpenTool(), @"{ ""spec"": ""{ broken"" }");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not valid JSON", result.Text);
        }

        [Test]
        public void SaveValidatesBeforeWriting()
        {
            PanelSaveTool tool = new PanelSaveTool();
            Assert.AreEqual(ToolKind.FileEdit, tool.Kind);
            Assert.IsTrue(Run(tool, @"{ ""name"": ""???"", ""spec"": { ""title"": ""T"", ""items"": [] } }").IsError);

            ToolResult invalid = Run(tool, @"{ ""name"": ""parley-test-invalid"", ""spec"": { ""items"": [] } }");
            Assert.IsTrue(invalid.IsError);
            StringAssert.Contains("nothing saved", invalid.Text);
        }

        [Test]
        public void ToolsAreRegisteredAsUnityTools()
        {
            ToolCatalog catalog = new ToolCatalog(ToolCatalog.CreateUnityTools());

            Assert.IsTrue(catalog.TryGet("panel_open", out IParleyTool open));
            Assert.AreEqual(ToolKind.ReadOnly, open.Kind);
            Assert.IsTrue(catalog.TryGet("panel_save", out _));
            Assert.IsTrue(catalog.TryGet("panel_list", out _));
        }

        private static ToolResult Run(IParleyTool tool, string input)
        {
            return tool.ExecuteAsync(JObject.Parse(input), new ToolContext(), CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
