using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class SdkMcpBridgeTests
    {
        [Test]
        public void AnswersJsonRpc()
        {
            SdkMcpBridge bridge = new SdkMcpBridge(new ToolCatalog(new IParleyTool[] { new EchoTool() }), () => new ToolContext());
            JObject init = Run(bridge.HandleAsync("unity", JObject.Parse("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\"}}"), CancellationToken.None));
            Assert.AreEqual("2025-03-26", (string)init["result"]["protocolVersion"]);

            JObject list = Run(bridge.HandleAsync("unity", JObject.Parse("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}"), CancellationToken.None));
            Assert.AreEqual("echo", (string)list["result"]["tools"][0]["name"]);

            JObject call = Run(bridge.HandleAsync("unity", JObject.Parse("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"echo\",\"arguments\":{\"text\":\"hi\"}}}"), CancellationToken.None));
            Assert.AreEqual("hi", (string)call["result"]["content"][0]["text"]);
            Assert.IsFalse((bool)call["result"]["isError"]);

            Assert.IsNull(Run(bridge.HandleAsync("unity", JObject.Parse("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}"), CancellationToken.None)));
            JObject unknown = Run(bridge.HandleAsync("other", JObject.Parse("{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/list\"}"), CancellationToken.None));
            Assert.AreEqual(-32601, (int)unknown["error"]["code"]);
        }

        [Test]
        public void McpConfigUsesSdkServerType()
        {
            JObject config = SdkMcpBridge.McpConfig();
            Assert.AreEqual("sdk", (string)config["mcpServers"]["unity"]["type"]);
        }

        private static T Run<T>(Task<T> task)
        {
            Assert.IsTrue(task.IsCompleted, "expected synchronous completion");
            return task.Result;
        }
    }
}