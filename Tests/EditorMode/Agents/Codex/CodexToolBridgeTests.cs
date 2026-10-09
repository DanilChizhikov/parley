using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexToolBridgeTests
    {
        [Test]
        public void SpecsUseUnityNamespace()
        {
            CodexToolBridge bridge = CreateBridge(new IParleyTool[] { new EchoTool() });
            JArray specs = bridge.Specs();
            Assert.IsTrue(bridge.HasTools);
            Assert.AreEqual("namespace", (string)specs[0]["type"]);
            Assert.AreEqual(CodexToolBridge.Namespace, (string)specs[0]["name"]);
            Assert.AreEqual("function", (string)specs[0]["tools"][0]["type"]);
            Assert.AreEqual("echo", (string)specs[0]["tools"][0]["name"]);
            Assert.IsNotNull(specs[0]["tools"][0]["inputSchema"]);
        }

        [Test]
        public void EmptyCatalogHasNoSpecs()
        {
            CodexToolBridge bridge = CreateBridge(new IParleyTool[0]);
            Assert.IsFalse(bridge.HasTools);
            Assert.AreEqual(0, bridge.Specs().Count);
        }

        [Test]
        public void CallRunsToolAndReportsSuccess()
        {
            CodexToolBridge bridge = CreateBridge(new IParleyTool[] { new EchoTool() });
            JObject response = Run(bridge.CallAsync(JObject.Parse("{\"namespace\":\"unity\",\"tool\":\"echo\",\"arguments\":{\"text\":\"hi\"}}"), CancellationToken.None));
            Assert.AreEqual("inputText", (string)response["contentItems"][0]["type"]);
            Assert.AreEqual("hi", (string)response["contentItems"][0]["text"]);
            Assert.IsTrue((bool)response["success"]);
        }

        [Test]
        public void UnknownToolOrNamespaceFails()
        {
            CodexToolBridge bridge = CreateBridge(new IParleyTool[] { new EchoTool() });
            JObject unknown = Run(bridge.CallAsync(JObject.Parse("{\"namespace\":\"unity\",\"tool\":\"nope\",\"arguments\":{}}"), CancellationToken.None));
            Assert.IsFalse((bool)unknown["success"]);
            StringAssert.Contains("Unknown tool", (string)unknown["contentItems"][0]["text"]);

            JObject foreign = Run(bridge.CallAsync(JObject.Parse("{\"namespace\":\"other\",\"tool\":\"echo\",\"arguments\":{\"text\":\"hi\"}}"), CancellationToken.None));
            Assert.IsFalse((bool)foreign["success"]);
        }

        [Test]
        public void DisplayNameMatchesLocalPrefix()
        {
            Assert.AreEqual(ToolCatalog.UnityLocalPrefix + "console", CodexToolBridge.DisplayName("console"));
        }

        private static CodexToolBridge CreateBridge(IParleyTool[] tools)
        {
            return new CodexToolBridge(new ToolCatalog(tools), () => new ToolContext());
        }

        private static T Run<T>(Task<T> task)
        {
            Assert.IsTrue(task.IsCompleted, "expected synchronous completion");
            return task.Result;
        }
    }
}